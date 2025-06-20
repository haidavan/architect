using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using MongoDB.Driver;
using Neo4j.Driver;
using Nest;
using StackExchange.Redis;
using Serilog;
using Serilog.Formatting.Json;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace DbCleaner;
public class DatabaseCleaner
{
    private readonly ILogger _logger;
    private readonly DatabaseConfig _config;

    private NpgsqlConnection _pgConnection;
    private IMongoDatabase _mongoDb;
    private IDriver _neo4jDriver;
    private IElasticClient _esClient;
    private IConnectionMultiplexer _redis;

    public DatabaseCleaner(DatabaseConfig config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> ConnectAllAsync()
    {
        try
        {
            // PostgreSQL
            _logger.LogInformation("Connecting to PostgreSQL...");
            var pgConnString = $"Host={_config.Postgres.Host};Port={_config.Postgres.Port};" +
                               $"Database={_config.Postgres.DbName};Username={_config.Postgres.User};" +
                               $"Password={_config.Postgres.Password}";
            _pgConnection = new NpgsqlConnection(pgConnString);
            await _pgConnection.OpenAsync();

            // MongoDB
            _logger.LogInformation("Connecting to MongoDB...");
            var mongoSettings = new MongoClientSettings
            {
                Server = new MongoServerAddress(_config.Mongo.Host, _config.Mongo.Port),
                Credential = MongoCredential.CreateCredential(
                    _config.Mongo.AuthDb,
                    _config.Mongo.User,
                    _config.Mongo.Password)
            };
            var mongoClient = new MongoClient(mongoSettings);
            _mongoDb = mongoClient.GetDatabase(_config.Mongo.DbName);

            // Neo4j
            _logger.LogInformation("Connecting to Neo4j...");
            _neo4jDriver = GraphDatabase.Driver(
                _config.Neo4j.Uri,
                AuthTokens.Basic(_config.Neo4j.User, _config.Neo4j.Password));

            // ElasticSearch
            _logger.LogInformation("Connecting to ElasticSearch...");
            var esUri = new Uri(_config.Elastic.Host);
            var settings = new ConnectionSettings(esUri)
                .BasicAuthentication(_config.Elastic.User, _config.Elastic.Password)
                .ServerCertificateValidationCallback((_, _, _, _) => true)
                .DefaultIndex("default");
            _esClient = new ElasticClient(settings);

            // Redis
            _logger.LogInformation("Connecting to Redis...");
            var redisConfig = new ConfigurationOptions
            {
                EndPoints = { $"{_config.Redis.Host}:{_config.Redis.Port}" },
                Password = _config.Redis.Password,
                AbortOnConnectFail = false,
                AllowAdmin = true // 👈 ADD THIS LINE
            };
            _redis = await ConnectionMultiplexer.ConnectAsync(redisConfig);

            _logger.LogInformation("All connections established successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection error");
            await CloseAllConnectionsAsync();
            return false;
        }
    }

    public async Task<bool> CleanPostgresAsync()
    {
        await using var transaction = await _pgConnection.BeginTransactionAsync();
        try
        {
            await using var cmd = _pgConnection.CreateCommand();
            cmd.Transaction = transaction;

            // Disable foreign key checks
            cmd.CommandText = "SET session_replication_role = 'replica';";
            await cmd.ExecuteNonQueryAsync();

            // Get all tables
            cmd.CommandText = @"SELECT table_name 
                              FROM information_schema.tables 
                              WHERE table_schema = 'public' 
                              AND table_type = 'BASE TABLE'";
            var tables = new List<string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    tables.Add(reader.GetString(0));
                }
            }

            if (tables.Count == 0)
            {
                _logger.LogInformation("PostgreSQL: No tables to clean");
                return true;
            }

            // Truncate all tables
            foreach (var table in tables)
            {
                cmd.CommandText = $"TRUNCATE TABLE \"{table}\" CASCADE";
                await cmd.ExecuteNonQueryAsync();
            }

            // Reset sequences
            cmd.CommandText = @"SELECT sequence_name 
                              FROM information_schema.sequences 
                              WHERE sequence_schema = 'public'";
            var sequences = new List<string>();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    sequences.Add(reader.GetString(0));
                }
            }

            foreach (var seq in sequences)
            {
                try
                {
                    cmd.CommandText = $"ALTER SEQUENCE \"{seq}\" RESTART WITH 1";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to reset sequence {seq}: {ex.Message}");
                }
            }

            // Re-enable foreign key checks
            cmd.CommandText = "SET session_replication_role = 'origin';";
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            _logger.LogInformation($"PostgreSQL: Cleared {tables.Count} tables and reset {sequences.Count} sequences");
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error cleaning PostgreSQL");
            return false;
        }
    }

    public async Task<bool> CleanMongoDbAsync()
    {
        try
        {
            var collections = (await _mongoDb.ListCollectionNamesAsync()).ToList();

            if (collections.Count == 0)
            {
                _logger.LogInformation("MongoDB: No collections to clean");
                return true;
            }

            foreach (var collection in collections)
            {
                await _mongoDb.GetCollection<object>(collection).DeleteManyAsync(FilterDefinition<object>.Empty);
            }

            _logger.LogInformation($"MongoDB: Cleared {collections.Count} collections");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning MongoDB");
            return false;
        }
    }

    public async Task<bool> CleanNeo4jAsync()
    {
        try
        {
            await using var session = _neo4jDriver.AsyncSession();
            var result = await session.RunAsync("MATCH (n) RETURN count(n) AS count");
            var record = await result.SingleAsync();
            var count = record["count"].As<long>();

            if (count == 0)
            {
                _logger.LogInformation("Neo4j: No data to clean");
                return true;
            }

            await session.RunAsync("MATCH (n) DETACH DELETE n");
            _logger.LogInformation($"Neo4j: Deleted {count} nodes");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning Neo4j");
            return false;
        }
    }

    public async Task<bool> CleanElasticsearchAsync()
    {
        try
        {
            var indices = (await _esClient.Cat.IndicesAsync()).Records
                .Where(i => !i.Index.StartsWith('.'))
                .Select(i => i.Index)
                .ToList();

            if (indices.Count == 0)
            {
                _logger.LogInformation("ElasticSearch: No indices to clean");
                return true;
            }

            var response = await _esClient.Indices.DeleteAsync(Indices.Index(indices));
            if (!response.IsValid)
            {
                throw new Exception(response.DebugInformation);
            }

            _logger.LogInformation($"ElasticSearch: Deleted {indices.Count} indices");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning ElasticSearch");
            return false;
        }
    }

    public async Task<bool> CleanRedisAsync()
    {
        try
        {
            var db = _redis.GetDatabase();
            var server = _redis.GetServers().First();
            var dbSize = await db.ExecuteAsync("DBSIZE");

            if (dbSize.IsNull || (long)dbSize == 0)
            {
                _logger.LogInformation("Redis: No data to clean");
                return true;
            }

            await server.FlushDatabaseAsync();
            _logger.LogInformation($"Redis: Cleared {dbSize} keys");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning Redis");
            return false;
        }
    }

    public async Task<bool> CleanAllDatabasesAsync()
    {
        if (!await ConnectAllAsync())
        {
            return false;
        }

        var results = new Dictionary<string, bool>
        {
            ["postgres"] = await CleanPostgresAsync(),
            ["mongo"] = await CleanMongoDbAsync(),
            ["neo4j"] = await CleanNeo4jAsync(),
            ["elastic"] = await CleanElasticsearchAsync(),
            ["redis"] = await CleanRedisAsync()
        };

        await CloseAllConnectionsAsync();

        if (results.Values.All(success => success))
        {
            _logger.LogInformation("All databases cleaned successfully!");
            return true;
        }

        var failed = results.Where(r => !r.Value).Select(r => r.Key);
        _logger.LogError($"Errors occurred while cleaning: {string.Join(", ", failed)}");
        return false;
    }

    public async Task CloseAllConnectionsAsync()
    {
        if (_pgConnection != null)
        {
            await _pgConnection.CloseAsync();
            _pgConnection.Dispose();
            _logger.LogInformation("PostgreSQL connection closed");
        }

        _neo4jDriver?.Dispose();
        _logger.LogInformation("Neo4j connection closed");

        _redis?.Dispose();
        _logger.LogInformation("Redis connection closed");
    }
}

// Configuration classes
public class DatabaseConfig
{
    public PostgresConfig Postgres { get; set; }
    public MongoConfig Mongo { get; set; }
    public Neo4jConfig Neo4j { get; set; }
    public ElasticConfig Elastic { get; set; }
    public RedisConfig Redis { get; set; }
}

public class PostgresConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string DbName { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
}

public class MongoConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string DbName { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
    public string AuthDb { get; set; } = "admin";
}

public class Neo4jConfig
{
    public string Uri { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
}

public class ElasticConfig
{
    public string Host { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
}

public class RedisConfig
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string Password { get; set; }
}
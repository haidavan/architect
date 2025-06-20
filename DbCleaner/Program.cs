// Program.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Json;

namespace DbCleaner;
class Program
{
    static async Task Main(string[] args)
    {
        // Configure logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog(new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console(new JsonFormatter())
                .CreateLogger());
        });
        var logger = loggerFactory.CreateLogger<DatabaseCleaner>();

        // Configuration matching docker-compose
        var config = new DatabaseConfig
        {
            Postgres = new PostgresConfig
            {
                Host = "localhost",
                Port = 5430,
                DbName = "postgres_db",
                User = "postgres_user",
                Password = "postgres_password"
            },
            Mongo = new MongoConfig
            {
                Host = "localhost",
                Port = 27017,
                DbName = "university_db",
                User = "admin",
                Password = "secret"
            },
            Neo4j = new Neo4jConfig
            {
                Uri = "bolt://localhost:7687",
                User = "neo4j",
                Password = "strongpassword"
            },
            Elastic = new ElasticConfig
            {
                Host = "http://localhost:9200",
                User = "elastic",
                Password = "secret"
            },
            Redis = new RedisConfig
            {
                Host = "localhost",
                Port = 6379,
                Password = ""
            }
        };

        var cleaner = new DatabaseCleaner(config, logger);
        if (await cleaner.CleanAllDatabasesAsync())
        {
            logger.LogInformation("All databases cleaned successfully!");
        }
        else
        {
            logger.LogError("Database cleaning completed with errors");
        }
    }
}
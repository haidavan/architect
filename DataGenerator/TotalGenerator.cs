using System;
using System.Threading.Tasks;

public class TotalGenerator
{
    private const string PgConnectionString =
        "Host=localhost;Port=5430;Database=postgres_db;Username=postgres_user;Password=postgres_password";

    private const string Neo4jUri = "bolt://localhost:7687";
    private const string Neo4jUser = "neo4j";
    private const string Neo4jPassword = "strongpassword";

    private const string MongoUri = "mongodb://admin:secret@localhost:27017";
    private const string RedisHost = "localhost";

    public async Task Run()
    {
        try
        {
            // 1. Создание схемы БД
            Console.WriteLine("🛠 Создание схемы PostgreSQL...");
            var schemaManager = new PostgresSchemaManager("localhost", 5430, "postgres_db", "postgres_user", "postgres_password");
            schemaManager.CreateSchema();

            // 2. Генерация тестовых данных
            Console.WriteLine("👥 Генерация тестовых данных...");
            using var generator = new RandomAttendanceGenerator(PgConnectionString);
            await generator.GenerateAllData(studentsPerGroup: 10);

            // 3. Синхронизация с Neo4j
            Console.WriteLine("🔄 Синхронизация с Neo4j...");
            var neo4jSync = new Neo4jSyncService(Neo4jUri, Neo4jUser, Neo4jPassword,
                                                "localhost", 5430, "postgres_db", "postgres_user", "postgres_password");
            await neo4jSync.SyncAll();
            Console.WriteLine("✅ Neo4j синхронизирован");

            // 4. Синхронизация с MongoDB
            Console.WriteLine("🔄 Синхронизация с MongoDB...");
            var mongoSync = new MongoSyncService();
            mongoSync.SyncPostgresToMongo(MongoUri, "university_db",
                                        "localhost", 5430, "postgres_db", "postgres_user", "postgres_password");
            Console.WriteLine("✅ MongoDB синхронизирована");

            // 5. Синхронизация с Redis
            Console.WriteLine("🔄 Синхронизация с Redis...");
            var redisSync = new RedisSyncService();
            redisSync.SyncStudentsToRedis(RedisHost, 6379,
                                        "localhost", 5430, "postgres_db", "postgres_user", "postgres_password");
            Console.WriteLine("✅ Redis синхронизирован");

            // 6. Синхронизация с Elasticsearch
            Console.WriteLine("🔄 Синхронизация с Elasticsearch...");
            var elasticSync = new ElasticsearchSyncService();
            elasticSync.GenerateAndSyncMaterials("localhost", 9200, "elastic", "secret",
                                               "localhost", 5430, "postgres_db", "postgres_user", "postgres_password",
                                               "./lecture_materials");
            Console.WriteLine("✅ Elasticsearch синхронизирован");

            Console.WriteLine("🎉 Все операции успешно завершены!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔥 Критическая ошибка: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
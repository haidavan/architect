using Dapper;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
using System.Text.Json;
using System.Threading.Tasks;

namespace UniversityApi.Services
{
    public interface IRedisService
    {
        Task<StudentRedisInfo> GetStudentInfo(int studentId);
    }

    public class RedisService : IRedisService
    {
        private readonly string _pgConnectionString;
        private readonly ILogger<RedisService> _logger;

        public RedisService(IConfiguration config, ILogger<RedisService> logger)
        {
            // PostgreSQL connection details only
            var pgHost = config["Postgres:Host"] ?? "localhost";
            var pgPort = config.GetValue<int>("Postgres:Port", 5430);
            var pgDb = config["Postgres:Database"] ?? "postgres_db";
            var pgUser = config["Postgres:Username"] ?? "postgres_user";
            var pgPassword = config["Postgres:Password"] ?? "postgres_password";
            _pgConnectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword}";
            _logger = logger;
        }

        public async Task<StudentRedisInfo> GetStudentInfo(int studentId)
        {
            // Directly fetch from PostgreSQL without Redis caching
            return await GetStudentFromDatabase(studentId) ?? new StudentRedisInfo();
        }

        private async Task<StudentRedisInfo> GetStudentFromDatabase(int studentId)
        {
            using var conn = new NpgsqlConnection(_pgConnectionString);
            try
            {
                await conn.OpenAsync();

                return await conn.QueryFirstOrDefaultAsync<StudentRedisInfo>(@"
                    SELECT 
                        name AS Name,
                        age AS Age,
                        mail AS Mail,
                        group_id AS Group
                    FROM students
                    WHERE id = @studentId",
                    new { studentId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching student {studentId} from PostgreSQL");
                return null;
            }
        }
    }

    public class StudentRedisInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Mail { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
    }
}
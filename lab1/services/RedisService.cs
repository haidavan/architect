using Microsoft.Extensions.Configuration;
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
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisService> _logger;

        public RedisService(IConfiguration config, ILogger<RedisService> logger)
        {
            var redisHost = config["Redis:Host"] ?? "localhost";
            var redisPort = config.GetValue<int>("Redis:Port", 6379);
            var connectionString = $"{redisHost}:{redisPort}";

            _redis = ConnectionMultiplexer.Connect(connectionString);
            _logger = logger;
        }

        public async Task<StudentRedisInfo> GetStudentInfo(int studentId)
        {
            var db = _redis.GetDatabase();
            var cacheKey = $"student:{studentId}";

            try
            {
                var json = await db.StringGetAsync(cacheKey);

                if (!json.IsNullOrEmpty)
                {
                    _logger.LogInformation($"Retrieved student {studentId} from Redis");
                    return JsonSerializer.Deserialize<StudentRedisInfo>(json);
                }

                _logger.LogWarning($"Student {studentId} not found in Redis");
                return new StudentRedisInfo();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Redis error for student {studentId}");
                return new StudentRedisInfo();
            }
        }
    }

    public class StudentRedisInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Mail { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
    }
}
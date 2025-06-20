using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniversityApi.Services
{
    public interface IAttendanceService
    {
        Task<List<Attendee>> FindWorstAttendees(
            List<int> lectureIds,
            DateTime startDate,
            DateTime endDate,
            int topN);
    }

    public class Attendee
    {
        public int StudentId { get; set; }
        public int AttendanceCount { get; set; }
    }
    public class AttendanceService : IAttendanceService
    {
        private readonly string _connectionString;
        private readonly ILogger<AttendanceService> _logger;

        public AttendanceService(IConfiguration config, ILogger<AttendanceService> logger)
        {
            var host = config["Postgres:Host"] ?? "localhost";
            var port = config.GetValue<int>("Postgres:Port", 5430);
            var db = config["Postgres:Database"] ?? "postgres_db";
            var user = config["Postgres:Username"] ?? "postgres_user";
            var password = config["Postgres:Password"] ?? "postgres_password";

            _connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={password}";
            _logger = logger;
        }

        public async Task<List<Attendee>> FindWorstAttendees(
            List<int> lectureIds,
            DateTime startDate,
            DateTime endDate,
            int topN)
        {
            if (lectureIds == null || !lectureIds.Any())
            {
                _logger.LogWarning("No lecture IDs provided for attendance search");
                return new List<Attendee>();
            }

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            try
            {
                _logger.LogInformation($"Finding worst attendees for {lectureIds.Count} lectures between " +
                    $"{startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}. Top {topN} results.");

                var query = @"
    SELECT 
        s.id AS StudentId, 
        COUNT(a.student_id) FILTER (WHERE a.attended) AS AttendanceCount
    FROM students s
    LEFT JOIN attendance a ON s.id = a.student_id
    LEFT JOIN schedule sc ON sc.id = a.schedule_id
        AND sc.lecture_id = ANY(@lectureIds)
        AND sc.date BETWEEN @startDate AND @endDate
    GROUP BY s.id
    ORDER BY AttendanceCount ASC
    LIMIT @topN";

                var parameters = new
                {
                    lectureIds,
                    startDate,
                    endDate,
                    topN
                };

                var result = (await conn.QueryAsync<Attendee>(query, parameters)).ToList();
                _logger.LogInformation($"Found {result.Count} attendees with low attendance");

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding worst attendees");
                throw;
            }
        }
    }
}
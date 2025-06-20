using Neo4j.Driver;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UniversityApi.Models;
using Dapper;

namespace UniversityApi.Services
{
    public interface IGroupReportService
    {
        Task<List<GroupReport>> GenerateGroupReport(int groupId);
    }

    public class GroupReportService : IGroupReportService, IDisposable
    {
        private readonly IDriver _neo4jDriver;
        private readonly string _pgConnectionString;
        private readonly ILogger<GroupReportService> _logger;

        public GroupReportService(IConfiguration config, ILogger<GroupReportService> logger)
        {
            // Neo4j configuration
            var neo4jHost = config["Neo4j:Host"] ?? "localhost";
            var neo4jPort = config.GetValue<int>("Neo4j:Port", 7687);
            var neo4jUser = config["Neo4j:User"] ?? "neo4j";
            var neo4jPassword = config["Neo4j:Password"] ?? "strongpassword";

            _neo4jDriver = GraphDatabase.Driver(
                $"bolt://{neo4jHost}:{neo4jPort}",
                AuthTokens.Basic(neo4jUser, neo4jPassword),
                o => o.WithEncryptionLevel(EncryptionLevel.None)
            );

            // PostgreSQL configuration
            var pgHost = config["Postgres:Host"] ?? "localhost";
            var pgPort = config.GetValue<int>("Postgres:Port", 5430);
            var pgDb = config["Postgres:Database"] ?? "postgres_db";
            var pgUser = config["Postgres:Username"] ?? "postgres_user";
            var pgPassword = config["Postgres:Password"] ?? "postgres_password";
            _pgConnectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword}";

            _logger = logger;
        }

        public async Task<List<GroupReport>> GenerateGroupReport(int groupId)
        {
            // 1. Get group and department info from Neo4j
            var (groupInfo, departmentId) = await GetGroupAndDepartmentInfo(groupId);
            if (groupInfo == null)
            {
                _logger.LogWarning($"Group {groupId} not found in Neo4j");
                return new List<GroupReport>();
            }

            // 2. Get students in group from Neo4j
            var students = await GetStudentsInGroup(groupId);
            if (!students.Any())
            {
                _logger.LogWarning($"No students found for group {groupId}");
                return new List<GroupReport>();
            }

            // 3. Get relevant schedules from Neo4j
            var schedules = await GetSchedulesForGroup(groupId, departmentId);
            if (!schedules.Any())
            {
                _logger.LogWarning($"No schedules found for group {groupId} and department {departmentId}");
            }

            // 4. Get attendance data from PostgreSQL
            var attendanceMap = await GetAttendanceHours(
                students.Select(s => s.Id).ToList(),
                schedules.Select(s => s.Id).ToList()
            );

            // 5. Generate report
            return GenerateReport(groupInfo, students, schedules, attendanceMap);
        }

        private async Task<(GroupInfo, int)> GetGroupAndDepartmentInfo(int groupId)
        {
            using var session = _neo4jDriver.AsyncSession();
            var cypher = @"
        MATCH (g:Group {id: $groupId})<-[:HAS_GROUP]-(spec:Specialty)<-[:HAS_SPECIALTY]-(dept:Department)
        RETURN 
            g.id AS groupId, 
            g.name AS groupName,
            dept.id AS deptId,
            dept.name AS deptName";

            var result = await session.RunAsync(cypher, new { groupId });
            var records = await result.ToListAsync();

            if (!records.Any()) return (null, 0);

            var record = records.First();
            return (new GroupInfo
            {
                Id = record["groupId"].As<int>(),
                Name = record["groupName"].As<string>(),
                Department = new DepartmentInfo
                {
                    Id = record["deptId"].As<int>(),
                    Name = record["deptName"].As<string>()
                }
            }, record["deptId"].As<int>());
        }

        private async Task<List<StudentInfo>> GetStudentsInGroup(int groupId)
        {
            using var session = _neo4jDriver.AsyncSession();
            var cypher = @"
                MATCH (g:Group {id: $groupId})-[:HAS_STUDENT]->(s:Student)
                RETURN s.id AS studentId, s.name AS studentName";

            var result = await session.RunAsync(cypher, new { groupId });
            var records = await result.ToListAsync();

            return records.Select(r => new StudentInfo
            {
                Id = r["studentId"].As<int>(),
                Name = r["studentName"].As<string>()
            }).ToList();
        }

        private async Task<List<ScheduleInfo>> GetSchedulesForGroup(int groupId, int departmentId)
        {
            using var session = _neo4jDriver.AsyncSession();
            var cypher = @"
                MATCH (g:Group {id: $groupId})<-[:HAS_GROUP]-(spec:Specialty)<-[:HAS_SPECIALTY]-(dept:Department {id: $departmentId})
                MATCH (dept)-[:OFFERS]->(c:Course)-[:HAS_LECTURE]->(l:Lecture)-[:SCHEDULED_AT]->(sch:Schedule)-[:FOR_GROUP]->(g)
                RETURN sch.id AS scheduleId";

            var result = await session.RunAsync(cypher, new { groupId, departmentId });
            var records = await result.ToListAsync();

            return records.Select(r => new ScheduleInfo
            {
                Id = r["scheduleId"].As<int>()
            }).ToList();
        }

        private async Task<Dictionary<(int, int), int>> GetAttendanceHours(List<int> studentIds, List<int> scheduleIds)
        {
            var attendanceMap = new Dictionary<(int, int), int>();

            if (!studentIds.Any() || !scheduleIds.Any())
                return attendanceMap;

            using var conn = new NpgsqlConnection(_pgConnectionString);
            await conn.OpenAsync();

            var query = @"
                SELECT student_id, schedule_id, 
                       SUM(CASE WHEN attended THEN 2 ELSE 0 END) AS hours
                FROM Attendance
                WHERE student_id = ANY(@studentIds)
                  AND schedule_id = ANY(@scheduleIds)
                GROUP BY student_id, schedule_id";

            var parameters = new
            {
                studentIds,
                scheduleIds
            };

            var result = await conn.QueryAsync<(int, int, int)>(query, parameters);

            foreach (var (studentId, scheduleId, hours) in result)
            {
                attendanceMap[(studentId, scheduleId)] = hours;
            }

            return attendanceMap;
        }

        private List<GroupReport> GenerateReport(
            GroupInfo groupInfo,
            List<StudentInfo> students,
            List<ScheduleInfo> schedules,
            Dictionary<(int, int), int> attendanceMap)
        {
            var totalPlannedHours = schedules.Count * 2; // 2 hours per lecture
            var reports = new List<GroupReport>();

            foreach (var student in students)
            {
                var attendedHours = schedules
                    .Where(s => attendanceMap.ContainsKey((student.Id, s.Id)))
                    .Sum(s => attendanceMap[(student.Id, s.Id)]);

                reports.Add(new GroupReport
                {
                    GroupInfo = groupInfo,
                    StudentInfo = student,
                    PlannedHours = totalPlannedHours,
                    AttendedHours = attendedHours
                });
            }

            return reports;
        }

        public void Dispose()
        {
            _neo4jDriver?.Dispose();
        }
    }

    internal class ScheduleInfo
    {
        public int Id { get; set; }
    }
}
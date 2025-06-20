using Neo4j.Driver;
using UniversityApi.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;

namespace UniversityApi.Services
{
    public interface INeo4jService
    {
        Task<List<AudienceReport>> GenerateAudienceReport(int year, int semester);
    }

    public class Neo4jService : INeo4jService, IDisposable
    {
        private readonly IDriver _driver;
        private readonly ILogger<Neo4jService> _logger;

        public Neo4jService(IConfiguration config, ILogger<Neo4jService> logger)
        {
            var host = config["Neo4j:Host"] ?? "localhost";
            var port = config.GetValue<int>("Neo4j:Port", 7687);
            var user = config["Neo4j:User"] ?? "neo4j";
            var password = config["Neo4j:Password"] ?? "strongpassword";

            _driver = GraphDatabase.Driver(
                $"bolt://{host}:{port}",
                AuthTokens.Basic(user, password),
                o => o.WithEncryptionLevel(EncryptionLevel.None)
            );
            _logger = logger;
        }

        public async Task<List<AudienceReport>> GenerateAudienceReport(int year, int semester)
        {
            (DateTime start, DateTime end) = CalculateSemesterDates(year, semester);

            var cypher = @"
        MATCH (sch:Schedule)
        WHERE sch.date >= date($start) AND sch.date <= date($end)
        MATCH (sch)-[:FOR_GROUP]->(g:Group)-[:HAS_STUDENT]->(s:Student)
        WITH sch, COUNT(DISTINCT s) AS total_students
        MATCH (l:Lecture)-[:SCHEDULED_AT]->(sch)
        MATCH (c:Course)-[:HAS_LECTURE]->(l)
        OPTIONAL MATCH (l)-[:HAS_MATERIAL]->(m:Material)
        RETURN
            c.name AS course_name,
            l.name AS lecture_name,
            COLLECT(DISTINCT m.name) AS tech_requirements,
            total_students
        ORDER BY course_name, lecture_name";

            using var session = _driver.AsyncSession();
            var result = await session.RunAsync(cypher, new
            {
                start = start.ToString("yyyy-MM-dd"),
                end = end.ToString("yyyy-MM-dd")
            });

            var reports = new List<AudienceReport>();
            await result.ForEachAsync(record =>
            {
                reports.Add(new AudienceReport
                {
                    CourseName = record["course_name"]?.As<string>() ?? "",
                    LectureName = record["lecture_name"]?.As<string>() ?? "",
                    TechRequirements = record["tech_requirements"]?.As<List<string>>() ?? new List<string>(),
                    TotalStudents = record["total_students"]?.As<int>() ?? 0
                });
            });

            return reports;
        }

        private (DateTime start, DateTime end) CalculateSemesterDates(int year, int semester)
        {
            return semester switch
            {
                2 => (new DateTime(year, 2, 1), new DateTime(year, 6, 30)),
                1 => (new DateTime(year, 9, 1), new DateTime(year + 1, 1, 31)),
                _ => throw new ArgumentException("Semester must be 1 or 2")
            };
        }

        public void Dispose()
        {
            _driver?.Dispose();
        }
    }

    public class AudienceReport
    {
        public string CourseName { get; set; }
        public string LectureName { get; set; }
        public List<string> TechRequirements { get; set; }
        public int TotalStudents { get; set; }
    }
}
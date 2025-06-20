// Services/ClassroomReportService.cs
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniversityApi.Models;

namespace UniversityApi.Services
{
    public interface IClassroomReportService
    {
        Task<List<ClassroomRequirementReport>> GenerateClassroomReport(string semester);
    }

    public class ClassroomReportService : IClassroomReportService
    {
        private readonly string _connectionString;

        public ClassroomReportService(IConfiguration config)
        {
            var pgHost = config["Postgres:Host"] ?? "localhost";
            var pgPort = config.GetValue<int>("Postgres:Port", 5430);
            var pgDb = config["Postgres:Database"] ?? "postgres_db";
            var pgUser = config["Postgres:Username"] ?? "postgres_user";
            var pgPassword = config["Postgres:Password"] ?? "postgres_password";

            _connectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword}";
        }

        public async Task<List<ClassroomRequirementReport>> GenerateClassroomReport(string semester)
        {
            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = @"
        WITH GroupStudents AS (
            SELECT group_id, COUNT(*) AS student_count
            FROM Students
            GROUP BY group_id
        ),
        LectureRequirements AS (
            SELECT
                l.id AS lecture_id,
                STRING_AGG(DISTINCT m.name, ', ') AS requirements
            FROM Lecture l
            LEFT JOIN Material_of_lecture m ON l.id = m.course_of_lecture_id
            GROUP BY l.id
        )
        SELECT
            c.id AS CourseId,
            c.name AS CourseName,
            l.id AS LectureId,
            l.name AS LectureName,
            COALESCE(lr.requirements, 'Стандартное оборудование') AS TechnicalRequirements,
            SUM(gs.student_count) AS StudentCount
        FROM Schedule s
        JOIN Lecture l ON s.lecture_id = l.id
        JOIN Course_of_lecture c ON l.course_of_lecture_id = c.id
        JOIN St_group g ON s.group_id = g.id
        JOIN GroupStudents gs ON g.id = gs.group_id
        LEFT JOIN LectureRequirements lr ON l.id = lr.lecture_id
        WHERE s.semester = @Semester
        GROUP BY c.id, c.name, l.id, l.name, lr.requirements
        ORDER BY c.name, l.name";

            return (await conn.QueryAsync<ClassroomRequirementReport>(query, new { Semester = semester }))
                .AsList();
        }
    }
}
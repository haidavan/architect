using Neo4j.Driver;
using Npgsql;
public class Neo4jSyncService : IDisposable
{
    private readonly IDriver _driver;
    private readonly string _pgConnectionString;

    public Neo4jSyncService(
        string neo4jUri,
        string neo4jUser,
        string neo4jPassword,
        string pgHost,
        int pgPort,
        string pgDb,
        string pgUser,
        string pgPassword)
    {
        _driver = GraphDatabase.Driver(
            neo4jUri,
            AuthTokens.Basic(neo4jUser, neo4jPassword),
            o => o.WithEncryptionLevel(EncryptionLevel.None)
        );
        _pgConnectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};" +
                              $"Username={pgUser};Password={pgPassword};";
    }

    public async Task SyncAll()
    {
        await ClearNeo4jDatabase();
        await SyncUniversities();
        await SyncInstitutes();
        await SyncDepartments();
        await SyncSpecialties();
        await SyncGroups();
        await SyncCourses();
        await SyncLectures();
        await SyncMaterials();
        await SyncSchedules();
        await SyncStudents();
        await SyncAttendance();
    }

    private async Task ClearNeo4jDatabase()
    {
        using var session = _driver.AsyncSession();
        await session.RunAsync("MATCH (n) DETACH DELETE n");
    }

    private async Task SyncUniversities()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT id, name, location FROM University", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var location = reader.IsDBNull(2) ? null : reader.GetString(2);

            await tx.RunAsync(
                "CREATE (u:University {id: $id, name: $name, location: $location})",
                new { id, name, location }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncInstitutes()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT id, name, university_id FROM Institute", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var universityId = reader.GetInt32(2);

            await tx.RunAsync(
                "MATCH (u:University {id: $uid}) " +
                "CREATE (i:Institute {id: $id, name: $name}) " +
                "CREATE (u)-[:HAS_INSTITUTE]->(i)",
                new { id, name, uid = universityId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncDepartments()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT id, name, institute_id FROM Department", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var instituteId = reader.GetInt32(2);

            await tx.RunAsync(
                "MATCH (i:Institute {id: $iid}) " +
                "CREATE (d:Department {id: $id, name: $name}) " +
                "CREATE (i)-[:HAS_DEPARTMENT]->(d)",
                new { id, name, iid = instituteId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncSpecialties()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT id, name, department_id FROM Specialty", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var departmentId = reader.GetInt32(2);

            await tx.RunAsync(
                "MATCH (d:Department {id: $did}) " +
                "CREATE (s:Specialty {id: $id, name: $name}) " +
                "CREATE (d)-[:HAS_SPECIALTY]->(s)",
                new { id, name, did = departmentId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncGroups()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand("SELECT id, name, speciality_id FROM St_group", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var specialityId = reader.GetInt32(2);

            await tx.RunAsync(
                "MATCH (s:Specialty {id: $sid}) " +
                "CREATE (g:Group {id: $id, name: $name}) " +
                "CREATE (s)-[:HAS_GROUP]->(g)",
                new { id, name, sid = specialityId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncCourses()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT id, name, department_id, specialty_id FROM Course_of_lecture",
            conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var deptId = reader.GetInt32(2);
            var specId = reader.GetInt32(3);

            await tx.RunAsync(
                "MATCH (d:Department {id: $did}), (s:Specialty {id: $sid}) " +
                "CREATE (c:Course {id: $id, name: $name}) " +
                "CREATE (d)-[:OFFERS]->(c) " +
                "CREATE (s)-[:INCLUDES_COURSE]->(c)",
                new { id, name, did = deptId, sid = specId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncLectures()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT id, name, course_of_lecture_id FROM Lecture",
            conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var courseId = reader.GetInt32(2);

            await tx.RunAsync(
                "MATCH (c:Course {id: $cid}) " +
                "CREATE (l:Lecture {id: $id, name: $name}) " +
                "CREATE (c)-[:HAS_LECTURE]->(l)",
                new { id, name, cid = courseId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncMaterials()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT id, name, course_of_lecture_id FROM Material_of_lecture",
            conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var courseId = reader.GetInt32(2);

            await tx.RunAsync(
                "MATCH (c:Course {id: $cid}) " +
                "CREATE (m:Material {id: $id, name: $name}) " +
                "CREATE (c)-[:HAS_MATERIAL]->(m)",
                new { id, name, cid = courseId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncSchedules()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT id, date, lecture_id, group_id FROM Schedule",
            conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var date = reader.GetDateTime(1);
            var lectureId = reader.GetInt32(2);
            var groupId = reader.GetInt32(3);

            await tx.RunAsync(
                "MATCH (l:Lecture {id: $lid}), (g:Group {id: $gid}) " +
                "CREATE (sch:Schedule {id: $id, date: date($date)}) " +
                "CREATE (l)-[:SCHEDULED_AT]->(sch) " +
                "CREATE (sch)-[:FOR_GROUP]->(g)",
                new
                {
                    id,
                    date = date.ToString("yyyy-MM-dd"),
                    lid = lectureId,
                    gid = groupId
                }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncStudents()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT id, name, age, mail, group_id FROM Students",
            conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var age = reader.GetInt32(2);
            var mail = reader.IsDBNull(3) ? null : reader.GetString(3);
            var groupId = reader.GetInt32(4);

            await tx.RunAsync(
                "MATCH (g:Group {id: $gid}) " +
                "CREATE (s:Student {id: $id, name: $name, age: $age, mail: $mail}) " +
                "CREATE (g)-[:HAS_STUDENT]->(s)",
                new { id, name, age, mail, gid = groupId }
            );
        }
        await tx.CommitAsync();
    }

    private async Task SyncAttendance()
    {
        using var conn = new NpgsqlConnection(_pgConnectionString);
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(
            "SELECT student_id, schedule_id, attended FROM Attendance",
            conn);
        using var reader = await cmd.ExecuteReaderAsync();
        using var session = _driver.AsyncSession();
        var tx = await session.BeginTransactionAsync();

        while (await reader.ReadAsync())
        {
            var studentId = reader.GetInt32(0);
            var scheduleId = reader.GetInt32(1);
            var attended = reader.GetBoolean(2);

            await tx.RunAsync(
                "MATCH (s:Student {id: $sid}), (sch:Schedule {id: $schid}) " +
                "CREATE (a:Attendance {student_id: $sid, schedule_id: $schid, attended: $attended}) " +
                "CREATE (s)-[:HAS_ATTENDANCE]->(a) " +
                "CREATE (a)-[:FOR_SCHEDULE]->(sch)",
                new
                {
                    sid = studentId,
                    schid = scheduleId,
                    attended
                }
            );
        }
        await tx.CommitAsync();
    }

    public void Dispose()
    {
        _driver?.Dispose();
        GC.SuppressFinalize(this);
    }
}

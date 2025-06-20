using Npgsql;
using StackExchange.Redis;
using System.Linq;

public class RedisSyncService
{
    public void SyncStudentsToRedis(string redisHost, int redisPort,
                                   string pgHost, int pgPort, string pgDb, string pgUser, string pgPassword)
    {
        var pgConnectionString = $"Host={pgHost};Port={pgPort};Database={pgDb};Username={pgUser};Password={pgPassword}";
        var redis = ConnectionMultiplexer.Connect($"{redisHost}:{redisPort}");
        var db = redis.GetDatabase();

        // Clear existing data
        var server = redis.GetServer(redis.GetEndPoints().First());
        foreach (var key in server.Keys(pattern: "student:*"))
            db.KeyDelete(key);
        foreach (var key in server.Keys(pattern: "index:student:*"))
            db.KeyDelete(key);

        using var pgConn = new NpgsqlConnection(pgConnectionString);
        pgConn.Open();

        using var cmd = new NpgsqlCommand(@"
            SELECT s.id, s.name, s.age, s.mail, g.name as group_name
            FROM Students s
            JOIN St_group g ON s.group_id = g.id", pgConn);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt32(0);
            var name = reader.GetString(1);
            var age = reader.GetInt32(2);
            var mail = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var group = reader.GetString(4);

            // Store student hash
            var key = $"student:{id}";
            db.HashSet(key, new HashEntry[]
            {
                new("id", id),
                new("name", name),
                new("age", age),
                new("mail", mail),
                new("group", group)
            });

            // Create indexes
            db.SetAdd($"index:student:name:{name.ToLower()}", id);
            if (!string.IsNullOrEmpty(mail))
                db.SetAdd($"index:student:email:{mail.ToLower()}", id);
            db.SetAdd($"index:student:group:{group.ToLower()}", id);

            // Full-text search indexes
            foreach (var term in $"{name} {mail} {group}".ToLower().Split().Distinct())
            {
                if (!string.IsNullOrWhiteSpace(term))
                    db.SetAdd($"index:student:search:{term}", id);
            }
        }

        Console.WriteLine($"Synced {server.Keys(pattern: "student:*").Count()} students to Redis");
    }
}

public class StudentSearch
{
    private readonly IDatabase _db;

    public StudentSearch(string host, int port)
    {
        var redis = ConnectionMultiplexer.Connect($"{host}:{port}");
        _db = redis.GetDatabase();
    }

    public HashEntry[] GetById(int studentId)
    {
        return _db.HashGetAll($"student:{studentId}");
    }

    public RedisValue[] SearchByName(string name)
    {
        return _db.SetMembers($"index:student:name:{name.ToLower()}");
    }

    public RedisValue[] SearchByEmail(string email)
    {
        return _db.SetMembers($"index:student:email:{email.ToLower()}");
    }

    public RedisValue[] SearchByGroup(string group)
    {
        return _db.SetMembers($"index:student:group:{group.ToLower()}");
    }

    public RedisValue[] FullTextSearch(string query)
    {
        var terms = query.ToLower().Split().Distinct();
        if (!terms.Any()) return new RedisValue[0];

        var keys = terms.Select(t => (RedisKey)$"index:student:search:{t}").ToArray();
        return _db.SetCombine(SetOperation.Intersect, keys);
    }
}
using Npgsql;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Collections.Generic;
using System.Linq;
using System;

public class MongoSyncService
{
    public void SyncPostgresToMongo(string mongoUri, string dbName,
                                   string pgHost, int pgPort, string pgDb, string pgUser, string pgPassword)
    {
        // 1. Загружаем все данные из PostgreSQL в память
        var allData = LoadAllDataFromPostgres(pgHost, pgPort, pgDb, pgUser, pgPassword);

        // 2. Подключение к MongoDB
        var client = new MongoClient(mongoUri);
        var database = client.GetDatabase(dbName);

        // Удаляем старую коллекцию
        database.DropCollection("universities");

        // Создаем новую коллекцию с валидацией схемы
        var command = new BsonDocument
        {
            { "create", "universities" },
            { "validator", new BsonDocument("$jsonSchema", new BsonDocument
                {
                    ["bsonType"] = "object",
                    ["required"] = new BsonArray { "name", "location", "institutes" },
                    ["properties"] = new BsonDocument
                    {
                        ["name"] = new BsonDocument("bsonType", "string"),
                        ["location"] = new BsonDocument("bsonType", "string"),
                        ["institutes"] = new BsonDocument
                        {
                            ["bsonType"] = "array",
                            ["items"] = new BsonDocument
                            {
                                ["bsonType"] = "object",
                                ["required"] = new BsonArray { "name", "departments" },
                                ["properties"] = new BsonDocument
                                {
                                    ["name"] = new BsonDocument("bsonType", "string"),
                                    ["departments"] = new BsonDocument
                                    {
                                        ["bsonType"] = "array",
                                        ["items"] = new BsonDocument
                                        {
                                            ["bsonType"] = "object",
                                            ["required"] = new BsonArray { "name" },
                                            ["properties"] = new BsonDocument
                                            {
                                                ["name"] = new BsonDocument("bsonType", "string"),
                                                ["specializations"] = new BsonDocument
                                                {
                                                    ["bsonType"] = "array",
                                                    ["items"] = new BsonDocument("bsonType", "string")
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                })
            }
        };

        database.RunCommand<BsonDocument>(command);
        var collection = database.GetCollection<BsonDocument>("universities");

        // 3. Строим документы MongoDB
        foreach (var university in allData.Universities)
        {
            var universityDoc = new BsonDocument
            {
                { "name", university.Name },
                { "location", university.Location },
                { "institutes", BuildInstitutes(allData, university.Id) }
            };

            collection.InsertOne(universityDoc);
        }

        Console.WriteLine($"✅ Synced {collection.EstimatedDocumentCount()} universities to MongoDB");
    }

    private UniversityData LoadAllDataFromPostgres(string host, int port, string db, string user, string password)
    {
        var data = new UniversityData();
        var connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={password}";

        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();

        // Загрузка университетов
        using (var cmd = new NpgsqlCommand("SELECT id, name, location FROM University", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                data.Universities.Add(new University(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2)
                ));
            }
        }

        // Загрузка институтов
        using (var cmd = new NpgsqlCommand("SELECT id, name, university_id FROM Institute", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                data.Institutes.Add(new Institute(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2)
                ));
            }
        }

        // Загрузка кафедр
        using (var cmd = new NpgsqlCommand("SELECT id, name, institute_id FROM Department", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                data.Departments.Add(new Department(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2)
                ));
            }
        }

        // Загрузка специализаций
        using (var cmd = new NpgsqlCommand("SELECT id, name, department_id FROM Specialty", conn))
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                data.Specialties.Add(new Specialty(
                    reader.GetInt32(0),
                    reader.GetString(1),
                    reader.GetInt32(2)
                ));
            }
        }

        return data;
    }

    private BsonArray BuildInstitutes(UniversityData data, int universityId)
    {
        var institutesArray = new BsonArray();

        foreach (var institute in data.Institutes.Where(i => i.UniversityId == universityId))
        {
            institutesArray.Add(new BsonDocument
            {
                { "name", institute.Name },
                { "departments", BuildDepartments(data, institute.Id) }
            });
        }

        return institutesArray;
    }

    private BsonArray BuildDepartments(UniversityData data, int instituteId)
    {
        var departmentsArray = new BsonArray();

        foreach (var department in data.Departments.Where(d => d.InstituteId == instituteId))
        {
            departmentsArray.Add(new BsonDocument
            {
                { "name", department.Name },
                { "specializations", BuildSpecializations(data, department.Id) }
            });
        }

        return departmentsArray;
    }

    private BsonArray BuildSpecializations(UniversityData data, int departmentId)
    {
        var specializations = new BsonArray();

        foreach (var spec in data.Specialties.Where(s => s.DepartmentId == departmentId))
        {
            specializations.Add(spec.Name);
        }

        return specializations;
    }

    private class UniversityData
    {
        public List<University> Universities { get; } = new();
        public List<Institute> Institutes { get; } = new();
        public List<Department> Departments { get; } = new();
        public List<Specialty> Specialties { get; } = new();
    }

    private record University(int Id, string Name, string Location);
    private record Institute(int Id, string Name, int UniversityId);
    private record Department(int Id, string Name, int InstituteId);
    private record Specialty(int Id, string Name, int DepartmentId);
}
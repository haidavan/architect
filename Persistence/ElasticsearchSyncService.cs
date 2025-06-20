using Bogus;
using Nest;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
// Добавляем псевдоним для решения конфликта имен
using FakerGenerator = Bogus.Faker;

public class ElasticsearchSyncService
{
    private FakerGenerator _faker;
    public ElasticsearchSyncService()
    {
        // Инициализация генератора с русской локалью
        _faker = new FakerGenerator("ru");
    }

    public void GenerateAndSyncMaterials(string esHost, int esPort, string esUser, string esPassword,
                                       string pgHost, int pgPort, string pgDb, string pgUser, string pgPassword,
                                       string materialsDir = "./lecture_materials")
    {
        // Для воспроизводимости результатов
        Bogus.Randomizer.Seed = new Random(42);
        var lectures = LoadLecturesFromPostgres(pgHost, pgPort, pgDb, pgUser, pgPassword);
        var settings = new ConnectionSettings(new Uri($"http://{esHost}:{esPort}"))
            .BasicAuthentication(esUser, esPassword)
            .ServerCertificateValidationCallback((_, _, _, _) => true)
            .DefaultIndex("lecture_materials");
        var client = new ElasticClient(settings);
        CreateElasticsearchIndex(client);
        Directory.CreateDirectory(materialsDir);
        GenerateAndSyncMaterials(client, lectures, materialsDir);
    }

    private List<LectureRecord> LoadLecturesFromPostgres(string host, int port, string db, string user, string password)
    {
        var lectures = new List<LectureRecord>();
        var connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={password}";
        using var conn = new NpgsqlConnection(connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(@"
            SELECT l.id, l.name, c.name as course_name
            FROM Lecture l
            JOIN Course_of_lecture c ON l.course_of_lecture_id = c.id", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lectures.Add(new LectureRecord(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2)
            ));
        }
        return lectures;
    }

    private void CreateElasticsearchIndex(ElasticClient client)
    {
        if (!client.Indices.Exists("lecture_materials").Exists)
        {
            client.Indices.Create("lecture_materials", c => c
                .Settings(s => s
                    .Analysis(a => a
                        .Analyzers(an => an
                            .Custom("russian", cu => cu
                                .Tokenizer("standard")
                                .Filters("lowercase", "russian_stop", "russian_stemmer")
                            )
                        )
                        .TokenFilters(tf => tf
                            .Stop("russian_stop", st => st
                                .StopWords("_russian_")
                            )
                            .Stemmer("russian_stemmer", sm => sm
                                .Language("russian")
                            )
                        )
                    )
                )
                .Map<LectureMaterial>(m => m
                    .Properties(p => p
                        .Number(n => n.Name(lm => lm.LectureId))
                        .Text(t => t.Name(lm => lm.LectureName)
                            .Analyzer("russian")
                            .Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Text(t => t.Name(lm => lm.CourseName)
                            .Analyzer("russian")
                            .Fields(f => f.Keyword(k => k.Name("keyword"))))
                        .Text(t => t.Name(lm => lm.Content).Analyzer("russian"))
                        .Keyword(k => k.Name(lm => lm.Keywords))
                        .Boolean(b => b.Name(lm => lm.GeneratedContent))
                    )
                )
            );
        }
    }

    private void GenerateAndSyncMaterials(ElasticClient client, List<LectureRecord> lectures, string materialsDir)
    {
        var academicTerms = new[]
        {
            "теория", "практика", "методология", "исследование", "анализ", "синтез", "гипотеза", "эксперимент",
            "формула", "уравнение", "концепция", "парадигма", "алгоритм", "модель", "структура", "система",
            "наблюдение", "верификация", "фальсификация", "индукция", "дедукция", "абстракция", "аксиома", "постулат",
            "корреляция", "регрессия", "статистика", "выборка", "репрезентативность", "валидность", "репликация",
            "интеграл", "дифференциал", "матрица", "вектор", "тензор", "топология", "граф", "множество",
            "изоморфизм", "гомоморфизм", "биекция", "инъекция", "сюръекция", "тождество", "константа", "переменная",
            "квант", "поле", "частица", "волна", "энтропия", "энергия", "масса", "заряд", "спин", "орбиталь",
            "валентность", "кристалл", "дифракция", "интерференция", "поляризация", "резонанс", "программа",
            "компилятор", "интерпретатор", "байт", "бит", "шифрование", "хеш", "автомат", "нейронная сеть",
            "градиент", "оптимизация", "композиция", "инкапсуляция", "наследование", "полиморфизм", "итерация",
            "клетка", "организм", "фермент", "катализатор", "реакция", "соединение", "молекула", "атом", "электрон",
            "протон", "нейтрон", "изотоп", "полимер", "мономер", "липид", "белок", "дискурс", "нарратив",
            "герменевтика", "феномен", "ноумен", "гносеология", "онтология", "диалектика", "семиотика", "синтагма",
            "парадигма", "интенция", "конструкция", "механизм", "привод", "трансмиссия", "устойчивость", "надежность",
            "прочность", "жесткость", "деформация", "напряжение", "усталость", "трение", "бифуркация", "аттрактор",
            "фрактал", "энтропия", "эмерджентность", "рекурсия", "инвариант", "топос", "морфизм", "функтор",
            "категорность", "гомология", "публикация", "рецензирование", "цитирование", "индексация", "аппликация",
            "аппроксимация", "итерация", "конвергенция", "дивергенция", "оптимизация", "максимизация", "минимизация"
        };

        foreach (var lecture in lectures)
        {
            var content = GenerateLectureContent(lecture, academicTerms);
            var keywords = GenerateKeywords(lecture, academicTerms);
            var material = new LectureMaterial
            {
                LectureId = lecture.Id,
                LectureName = lecture.Name,
                CourseName = lecture.Course,
                Content = content,
                Keywords = keywords,
                GeneratedContent = true
            };
            client.Index(material, idx => idx.Id(lecture.Id));
        }
        client.Indices.Refresh("lecture_materials");
        Console.WriteLine($"✅ Сгенерировано и синхронизировано материалов: {lectures.Count}");
        Console.WriteLine($"Файлы не сохраняются, как указано в задаче.");
    }

    private string GenerateLectureContent(LectureRecord lecture, string[] terms)
    {
        var content = $@"Лекция: {lecture.Name}
Курс: {lecture.Course}
Преподаватель: {_faker.Name.FullName()}
Основные понятия:
{string.Join("\n\n", _faker.Lorem.Paragraphs(3))}
Теоретическая часть:
{string.Join("\n\n", _faker.Lorem.Paragraphs(5))}
Практическое применение:
{string.Join("\n\n", _faker.Lorem.Paragraphs(4))}
Рекомендуемая литература:
1. {_faker.Commerce.ProductName()} / {_faker.Name.FullName()}
2. {_faker.Company.CatchPhrase()} / {_faker.Name.FullName()}";
        var sentences = content.Split('.');
        for (int i = 0; i < Math.Min(3, sentences.Length - 1); i++)
        {
            sentences[i] = sentences[i] + $" {terms[i]}.";
        }
        content = string.Join(".", sentences);
        return content;
    }

    private string[] GenerateKeywords(LectureRecord lecture, string[] terms)
    {
        var keywords = new List<string>();
        keywords.AddRange(lecture.Name.ToLower().Split(' '));
        keywords.AddRange(lecture.Course.ToLower().Split(' '));
        keywords.AddRange(_faker.Random.WordsArray(3));
        keywords.AddRange(terms.Take(2));
        return keywords
            .Distinct()
            .ToArray();
    }

    private record LectureRecord(int Id, string Name, string Course);
}

public class LectureMaterial
{
    public int LectureId { get; set; }
    public string LectureName { get; set; }
    public string CourseName { get; set; }
    public string Content { get; set; }
    public string[] Keywords { get; set; }
    public bool GeneratedContent { get; set; }
}

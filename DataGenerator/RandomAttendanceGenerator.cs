using Core;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class RandomAttendanceGenerator : IDisposable
{
    private readonly NpgsqlConnection _conn;
    private bool _disposed = false;
    private readonly Random _random = new Random();

    // Детализированные данные
    private readonly List<(string Name, string Location)> _universities = new()
{
    ("Московский государственный университет", "Москва"),
    ("Санкт-Петербургский государственный университет", "Санкт-Петербург"),
    ("Новосибирский государственный университет", "Новосибирск"),
    ("Казанский федеральный университет", "Казань"),
    ("Уральский федеральный университет", "Екатеринбург"),
    ("Национальный исследовательский ядерный университет МИФИ", "Москва"),
    ("Московский физико-технический институт", "Долгопрудный"),
    ("Высшая школа экономики", "Москва"),
    ("Московский государственный технический университет им. Н.Э. Баумана", "Москва"),
    ("Санкт-Петербургский политехнический университет Петра Великого", "Санкт-Петербург"),
    ("Томский государственный университет", "Томск"),
    ("Дальневосточный федеральный университет", "Владивосток"),
    ("Южный федеральный университет", "Ростов-на-Дону"),
    ("Сибирский федеральный университет", "Красноярск"),
    ("Российский университет дружбы народов", "Москва"),
    ("Финансовый университет при Правительстве РФ", "Москва"),
    ("Московский авиационный институт", "Москва"),
    ("Санкт-Петербургский государственный электротехнический университет", "Санкт-Петербург"),
    ("Нижегородский государственный университет им. Н.И. Лобачевского", "Нижний Новгород"),
    ("Самарский национальный исследовательский университет", "Самара"),
    ("Балтийский федеральный университет им. И. Канта", "Калининград"),
    ("Российский государственный гуманитарный университет", "Москва"),
    ("Московский государственный лингвистический университет", "Москва"),
    ("Российский экономический университет им. Г.В. Плеханова", "Москва"),
    ("Московский государственный строительный университет", "Москва"),
    ("Санкт-Петербургский государственный университет телекоммуникаций", "Санкт-Петербург"),
    ("Томский политехнический университет", "Томск"),
    ("Уфимский государственный авиационный технический университет", "Уфа"),
    ("Воронежский государственный университет", "Воронеж"),
    ("Пермский государственный национальный исследовательский университет", "Пермь"),
    ("Кубанский государственный университет", "Краснодар")
};

    private readonly List<(string Name, int UniversityId)> _institutes = new()
{
    ("Институт механики", 1),
    ("Физический факультет", 1),
    ("Химический факультет", 1),
    ("Институт вычислительной математики", 1),
    ("Юридический факультет", 2),
    ("Филологический факультет", 2),
    ("Институт истории", 2),
    ("Экономический факультет", 3),
    ("Факультет информационных технологий", 3),
    ("Биологический факультет", 4),
    ("Институт геологии", 4),
    ("Факультет психологии", 5),
    ("Институт материаловедения", 5),
    ("Факультет прикладной математики", 6),
    ("Институт ядерной физики", 6),
    ("Факультет аэрокосмических технологий", 7),
    ("Институт радиотехники", 7),
    ("Факультет бизнес-информатики", 8),
    ("Институт статистики", 8),
    ("Факультет машиностроения", 9),
    ("Институт энергетики", 9),
    ("Факультет компьютерных наук", 10),
    ("Институт робототехники", 10),
    ("Факультет международных отношений", 11),
    ("Институт нефти и газа", 11),
    ("Факультет архитектуры", 12),
    ("Институт морских технологий", 12),
    ("Факультет журналистики", 13),
    ("Институт искусств", 13),
    ("Факультет пищевых технологий", 14),
    ("Институт экологии", 14)
};

    private readonly List<(string Name, int InstituteId)> _departments = new()
{
    ("Кафедра теоретической механики", 1),
    ("Кафедра гидродинамики", 1),
    ("Кафедра квантовой физики", 2),
    ("Кафедра твердого тела", 2),
    ("Кафедра органической химии", 3),
    ("Кафедра неорганической химии", 3),
    ("Кафедра вычислительных методов", 4),
    ("Кафедра математического моделирования", 4),
    ("Кафедра гражданского права", 5),
    ("Кафедра уголовного права", 5),
    ("Кафедра русской литературы", 6),
    ("Кафедра зарубежной литературы", 6),
    ("Кафедра древней истории", 7),
    ("Кафедра современной истории", 7),
    ("Кафедра макроэкономики", 8),
    ("Кафедра микроэкономики", 8),
    ("Кафедра искусственного интеллекта", 9),
    ("Кафедра системного анализа", 9),
    ("Кафедра генетики", 10),
    ("Кафедра биохимии", 10),
    ("Кафедра минералогии", 11),
    ("Кафедра геофизики", 11),
    ("Кафедра клинической психологии", 12),
    ("Кафедра социальной психологии", 12),
    ("Кафедра композитных материалов", 13),
    ("Кафедра нанотехнологий", 13),
    ("Кафедра дифференциальных уравнений", 14),
    ("Кафедра теории вероятностей", 14),
    ("Кафедра ядерных реакторов", 15),
    ("Кафедра радиационной безопасности", 15)
};

    private readonly List<(string Name, int DepartmentId)> _specialties = new()
{
    ("Теоретическая механика", 1),
    ("Гидроаэродинамика", 2),
    ("Квантовая оптика", 3),
    ("Физика полупроводников", 4),
    ("Органический синтез", 5),
    ("Координационная химия", 6),
    ("Численные методы", 7),
    ("Математическое моделирование в механике", 8),
    ("Гражданское право", 9),
    ("Уголовное право", 10),
    ("Русская литература XIX века", 11),
    ("Современная зарубежная литература", 12),
    ("История древнего мира", 13),
    ("История России XX века", 14),
    ("Макроэкономический анализ", 15),
    ("Экономика фирмы", 16),
    ("Машинное обучение", 17),
    ("Системный анализ в экономике", 18),
    ("Генетика человека", 19),
    ("Молекулярная биология", 20),
    ("Минералогия и петрография", 21),
    ("Сейсмология", 22),
    ("Клиническая психология", 23),
    ("Организационная психология", 24),
    ("Композитные материалы в авиации", 25),
    ("Нанотехнологии в медицине", 26),
    ("Дифференциальные уравнения в физике", 27),
    ("Теория вероятностей и математическая статистика", 28),
    ("Ядерные энергетические установки", 29),
    ("Радиационная безопасность", 30)
};

    private readonly List<(string Name, int SpecialtyId)> _groups = new()
{
    ("МЕХ-101", 1),
    ("МЕХ-102", 1),
    ("ГИД-201", 2),
    ("ГИД-202", 2),
    ("КВАНТ-301", 3),
    ("ФИЗ-302", 4),
    ("ОРГ-401", 5),
    ("НЕОРГ-402", 6),
    ("ВМ-501", 7),
    ("ММ-502", 8),
    ("ГРАЖ-601", 9),
    ("УГОЛ-602", 10),
    ("РУСЛ-701", 11),
    ("ЗАРЛ-702", 12),
    ("ИСТД-801", 13),
    ("ИСТР-802", 14),
    ("МАКРО-901", 15),
    ("МИКРО-902", 16),
    ("МО-1001", 17),
    ("СА-1002", 18),
    ("ГЕН-1101", 19),
    ("МОЛБ-1102", 20),
    ("МИН-1201", 21),
    ("СЕЙСМ-1202", 22),
    ("КЛИН-1301", 23),
    ("ОРГП-1302", 24),
    ("КОМП-1401", 25),
    ("НАН-1402", 26),
    ("ДИФ-1501", 27),
    ("ТВ-1502", 28),
    ("ЯДР-1601", 29),
    ("РАД-1602", 30)
};

    private readonly List<(string Name, int DepartmentId, int SpecialtyId)> _courses = new()
{
    ("Теоретическая механика", 1, 1),
    ("Гидродинамика", 2, 2),
    ("Квантовая теория", 3, 3),
    ("Физика твердого тела", 4, 4),
    ("Органическая химия", 5, 5),
    ("Неорганическая химия", 6, 6),
    ("Численные методы", 7, 7),
    ("Математическое моделирование", 8, 8),
    ("Гражданское право", 9, 9),
    ("Уголовное право", 10, 10),
    ("История русской литературы", 11, 11),
    ("Современная зарубежная литература", 12, 12),
    ("История древнего мира", 13, 13),
    ("История России XX века", 14, 14),
    ("Макроэкономика", 15, 15),
    ("Микроэкономика", 16, 16),
    ("Машинное обучение", 17, 17),
    ("Системный анализ", 18, 18),
    ("Генетика", 19, 19),
    ("Молекулярная биология", 20, 20),
    ("Минералогия", 21, 21),
    ("Геофизика", 22, 22),
    ("Клиническая психология", 23, 23),
    ("Организационная психология", 24, 24),
    ("Композитные материалы", 25, 25),
    ("Нанотехнологии", 26, 26),
    ("Дифференциальные уравнения", 27, 27),
    ("Теория вероятностей", 28, 28),
    ("Ядерная физика", 29, 29),
    ("Радиационная безопасность", 30, 30)
};

    private readonly List<(string Name, int CourseId)> _lectures = new()
{
    ("Кинематика точки", 1),
    ("Динамика системы", 1),
    ("Уравнения Навье-Стокса", 2),
    ("Течения вязкой жидкости", 2),
    ("Уравнение Шредингера", 3),
    ("Квантовые состояния", 3),
    ("Кристаллическая решетка", 4),
    ("Дефекты кристаллов", 4),
    ("Реакции замещения", 5),
    ("Ароматические соединения", 5),
    ("Комплексные соединения", 6),
    ("Координационные числа", 6),
    ("Метод конечных разностей", 7),
    ("Интерполяция", 7),
    ("Моделирование механических систем", 8),
    ("Вероятностные модели", 8),
    ("Договорные обязательства", 9),
    ("Наследственное право", 9),
    ("Преступления против личности", 10),
    ("Уголовная ответственность", 10)
};

    private readonly List<(string Name, int CourseId)> _materials = new()
{
    ("Презентация по кинематике", 1),
    ("Задачи по динамике", 1),
    ("Лабораторная работа по гидродинамике", 2),
    ("Расчетные таблицы", 2),
    ("Конспект по квантовой теории", 3),
    ("Дополнительные материалы", 3),
    ("Слайды по кристаллографии", 4),
    ("Видео экспериментов", 4),
    ("Методичка по органике", 5),
    ("Тесты по реакциям", 5),
    ("Справочник по неорганике", 6),
    ("Таблицы свойств", 6),
    ("Программы для расчетов", 7),
    ("Примеры кода", 7),
    ("Шаблоны моделей", 8),
    ("Базы данных", 8),
    ("Нормативные акты", 9),
    ("Судебная практика", 9),
    ("Уголовный кодекс", 10),
    ("Комментарии к статьям", 10)
};

    public RandomAttendanceGenerator(string connectionString)
    {
        _conn = new NpgsqlConnection(connectionString);
        _conn.Open();
    }

    public async Task GenerateAllData(int studentsPerGroup = 10)
    {
        using var transaction = await _conn.BeginTransactionAsync();
        try
        {
            Console.WriteLine("Генерация справочных данных...");
            await GenerateReferenceData();

            Console.WriteLine("Генерация расписания...");
            await GenerateSchedule();

            Console.WriteLine("Генерация студентов и посещаемости...");
            await GenerateStudentsAndAttendance(studentsPerGroup);

            await transaction.CommitAsync();
            Console.WriteLine("✅ Данные успешно сгенерированы");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"❌ Ошибка генерации данных: {ex.Message}");
            throw;
        }
    }

    private async Task GenerateReferenceData()
    {
        // Университеты
        foreach (var university in _universities)
        {
            string name = university.Name;
            string location = university.Location;
            await ExecuteCommandAsync(
                "INSERT INTO University (name, location) VALUES (@name, @location)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("location", location)
            );
        }

        // Институты
        foreach (var institute in _institutes)
        {
            string name = institute.Name;
            int universityId = institute.UniversityId;
            await ExecuteCommandAsync(
                "INSERT INTO Institute (name, university_id) VALUES (@name, @universityId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("universityId", universityId)
            );
        }

        // Кафедры
        foreach (var department in _departments)
        {
            string name = department.Name;
            int instituteId = department.InstituteId;
            await ExecuteCommandAsync(
                "INSERT INTO Department (name, institute_id) VALUES (@name, @instituteId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("instituteId", instituteId)
            );
        }

        // Специальности
        foreach (var specialty in _specialties)
        {
            string name = specialty.Name;
            int departmentId = specialty.DepartmentId;
            await ExecuteCommandAsync(
                "INSERT INTO Specialty (name, department_id) VALUES (@name, @departmentId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("departmentId", departmentId)
            );
        }

        // Группы
        foreach (var group in _groups)
        {
            string name = group.Name;
            int specialtyId = group.SpecialtyId;
            await ExecuteCommandAsync(
                "INSERT INTO St_group (name, speciality_id) VALUES (@name, @specialtyId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("specialtyId", specialtyId)
            );
        }

        // Курсы лекций
        foreach (var course in _courses)
        {
            string name = course.Name;
            int deptId = course.DepartmentId;
            int specId = course.SpecialtyId;
            await ExecuteCommandAsync(
                "INSERT INTO Course_of_lecture (name, department_id, specialty_id) VALUES (@name, @deptId, @specId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("deptId", deptId),
                new NpgsqlParameter("specId", specId)
            );
        }

        // Лекции
        foreach (var lecture in _lectures)
        {
            string name = lecture.Name;
            int courseId = lecture.CourseId;
            await ExecuteCommandAsync(
                "INSERT INTO Lecture (name, course_of_lecture_id) VALUES (@name, @courseId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("courseId", courseId)
            );
        }

        // Материалы лекций
        foreach (var material in _materials)
        {
            string name = material.Name;
            int courseId = material.CourseId;
            await ExecuteCommandAsync(
                "INSERT INTO Material_of_lecture (name, course_of_lecture_id) VALUES (@name, @courseId)",
                new NpgsqlParameter("name", name),
                new NpgsqlParameter("courseId", courseId)
            );
        }
    }

    private async Task GenerateSchedule()
    {
        var schedules = new List<(DateTime Date, int LectureId, int GroupId)>();
        var groupIds = await GetIdsAsync("SELECT id FROM St_group");

        // Периоды семестров с фиксированными 5 занятиями на группу
        var semesters = new List<(DateTime Start, DateTime End, string Name)>
    {
        (new DateTime(2022, 9, 1), new DateTime(2022, 12, 20), "2022_fall"),
        (new DateTime(2023, 2, 1), new DateTime(2023, 5, 31), "2023_spring"),
        (new DateTime(2023, 9, 1), new DateTime(2023, 12, 20), "2023_fall"),
        (new DateTime(2024, 2, 1), new DateTime(2024, 5, 31), "2024_spring"),
        (new DateTime(2024, 9, 1), new DateTime(2024, 12, 20), "2024_fall"),
        (new DateTime(2025, 2, 1), new DateTime(2025, 5, 31), "2025_spring")
    };

        foreach (var semester in semesters)
        {
            // Рабочие дни в семестре (без выходных)
            var workDays = new List<DateTime>();
            for (var date = semester.Start; date <= semester.End; date = date.AddDays(1))
            {
                if (date.DayOfWeek != DayOfWeek.Saturday &&
                    date.DayOfWeek != DayOfWeek.Sunday)
                {
                    workDays.Add(date);
                }
            }

            // Для каждой группы создаем ровно 5 занятий
            foreach (var groupId in groupIds)
            {
                // Выбираем случайные 5 рабочих дней
                var selectedDays = workDays
                    .OrderBy(x => _random.Next())
                    .Take(5)
                    .ToList();

                foreach (var day in selectedDays)
                {
                    // Выбираем случайное время занятия (утро/день)
                    var timeSlot = day.AddHours(_random.Next(9, 15)).AddMinutes(_random.Next(0, 60));

                    // Выбираем случайную лекцию
                    var lectureId = _random.Next(1, _lectures.Count + 1);

                    schedules.Add((timeSlot, lectureId, groupId));
                }
            }
        }

        // Вставка расписания в БД
        foreach (var s in schedules)
        {
            await ExecuteCommandAsync(
                "INSERT INTO Schedule (date, lecture_id, group_id) VALUES (@date, @lectureId, @groupId)",
                new NpgsqlParameter("date", s.Date),
                new NpgsqlParameter("lectureId", s.LectureId),
                new NpgsqlParameter("groupId", s.GroupId)
            );
        }
    }

    private async Task GenerateStudentsAndAttendance(int studentsPerGroup)
    {
        // Создаем партиции для всех семестров
        var allSemesters = new[]
        {
        "2022_fall", "2023_spring", "2023_fall",
        "2024_spring", "2024_fall", "2025_spring"
    };

        foreach (var sem in allSemesters)
        {
            await ExecuteCommandAsync($"SELECT ensure_attendance_partition('{sem}')");
        }

        var groupIds = await GetIdsAsync("SELECT id FROM St_group");

        foreach (var groupId in groupIds)
        {
            // Получаем расписание группы
            var schedules = new List<(int Id, DateTime Date)>();
            await using (var cmd = new NpgsqlCommand(
                "SELECT id, date FROM Schedule WHERE group_id = @groupId", _conn))
            {
                cmd.Parameters.AddWithValue("groupId", groupId);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    schedules.Add((reader.GetInt32(0), reader.GetDateTime(1)));
                }
            }

            // Генерация студентов
            for (int i = 0; i < studentsPerGroup; i++)
            {
                var name = $"stud{_random.Next(10000, 99999)}";
                var age = _random.Next(17, 25);
                var mail = $"{name}@university.example";

                int studentId;
                await using (var cmd = new NpgsqlCommand(
                    "INSERT INTO Students (name, age, mail, group_id) VALUES (@name, @age, @mail, @groupId) RETURNING id",
                    _conn))
                {
                    cmd.Parameters.AddWithValue("name", name);
                    cmd.Parameters.AddWithValue("age", age);
                    cmd.Parameters.AddWithValue("mail", mail);
                    cmd.Parameters.AddWithValue("groupId", groupId);
                    studentId = (int)(await cmd.ExecuteScalarAsync())!;
                }

                // Генерация посещаемости
                foreach (var schedule in schedules)
                {
                    string semester = schedule.Date switch
                    {
                        { Month: >= 9 or 1 } => $"{schedule.Date.Year}_fall",
                        { Month: >= 2 and <= 6 } => $"{schedule.Date.Year}_spring",
                        _ => throw new Exception($"Invalid month: {schedule.Date.Month}")
                    };

                    bool attended = _random.NextDouble() < 0.7;

                    await ExecuteCommandAsync(
                        "INSERT INTO Attendance (student_id, schedule_id, attended, semester) " +
                        "VALUES (@studentId, @scheduleId, @attended, @semester)",
                        new NpgsqlParameter("studentId", studentId),
                        new NpgsqlParameter("scheduleId", schedule.Id),
                        new NpgsqlParameter("attended", attended),
                        new NpgsqlParameter("semester", semester)
                    );
                }
            }
        }
    }

    private List<int> GetRandomSample(List<(int Id, DateTime Date)> source, int count)
    {
        var result = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int index = _random.Next(0, source.Count);
            result.Add(source[index].Id);
        }
        return result;
    }

    private async Task<List<int>> GetIdsAsync(string sql)
    {
        var ids = new List<int>();
        await using var cmd = new NpgsqlCommand(sql, _conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            ids.Add(reader.GetInt32(0));
        }
        return ids;
    }

    private async Task ExecuteCommandAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var cmd = new NpgsqlCommand(sql, _conn);
        cmd.Parameters.AddRange(parameters);
        await cmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _conn?.Close();
            _conn?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
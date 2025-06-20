using Npgsql;

public class PostgresSchemaManager
{
    private readonly string _connectionString;

    public PostgresSchemaManager(string host, int port, string db, string user, string password)
    {
        _connectionString = $"Host={host};Port={port};Database={db};Username={user};Password={password}";
    }

    public void CreateSchema()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            -- 1. Reference tables
            CREATE TABLE IF NOT EXISTS University (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                location VARCHAR(100)
            );

            CREATE TABLE IF NOT EXISTS Institute (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                university_id INTEGER REFERENCES University(id)
            );

            CREATE TABLE IF NOT EXISTS Department (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                institute_id INTEGER REFERENCES Institute(id)
            );

            CREATE TABLE IF NOT EXISTS Specialty (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                department_id INTEGER REFERENCES Department(id)
            );

            CREATE TABLE IF NOT EXISTS St_group (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                speciality_id INTEGER REFERENCES Specialty(id)
            );

            CREATE TABLE IF NOT EXISTS Course_of_lecture (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                department_id INTEGER REFERENCES Department(id),
                specialty_id INTEGER REFERENCES Specialty(id)
            );

            CREATE TABLE IF NOT EXISTS Lecture (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                course_of_lecture_id INTEGER REFERENCES Course_of_lecture(id)
            );

            CREATE TABLE IF NOT EXISTS Material_of_lecture (
                id SERIAL PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                course_of_lecture_id INTEGER REFERENCES Lecture(id)
            );

            -- 2. Schedule with computed semester
            CREATE TABLE IF NOT EXISTS Schedule (
                id         SERIAL PRIMARY KEY,
                date       TIMESTAMP NOT NULL,
                lecture_id INTEGER REFERENCES Lecture(id),
                group_id   INTEGER REFERENCES St_group(id),
                semester   TEXT
            );
            
            CREATE OR REPLACE FUNCTION trg_compute_schedule_semester() RETURNS TRIGGER
                LANGUAGE plpgsql AS $$
                BEGIN
                NEW.semester :=
                    CASE
                    WHEN EXTRACT(MONTH FROM NEW.date) BETWEEN 1 AND 6
                        THEN (EXTRACT(YEAR FROM NEW.date)::INT || '_spring')
                    ELSE (EXTRACT(YEAR FROM NEW.date)::INT || '_fall')
                    END;
                RETURN NEW;
                END;
                $$;
            
            DROP TRIGGER IF EXISTS schedule_set_semester ON Schedule;
            CREATE TRIGGER schedule_set_semester
            BEFORE INSERT OR UPDATE OF date
            ON Schedule
            FOR EACH ROW
            EXECUTE FUNCTION trg_compute_schedule_semester();

            -- 3. Students
            CREATE TABLE IF NOT EXISTS Students (
                id       SERIAL PRIMARY KEY,
                name     VARCHAR(100) NOT NULL,
                age      INTEGER,
                mail     VARCHAR(100),
                group_id INTEGER REFERENCES St_group(id)
            );

            -- 4. Partitioned attendance
            CREATE TABLE IF NOT EXISTS Attendance (
                student_id  INTEGER NOT NULL REFERENCES Students(id),
                schedule_id INTEGER NOT NULL REFERENCES Schedule(id),
                attended    BOOLEAN NOT NULL,
                semester    TEXT NOT NULL
            ) PARTITION BY LIST (semester);

            -- 5. Trigger for attendance semester
            CREATE OR REPLACE FUNCTION trg_set_attendance_semester() RETURNS TRIGGER AS $$
            DECLARE
                rec_date TIMESTAMP;
                sem_val  TEXT;
            BEGIN
                SELECT date INTO rec_date
                FROM Schedule
                WHERE id = NEW.schedule_id;

                IF rec_date IS NULL THEN
                    RAISE EXCEPTION 'Schedule % not found', NEW.schedule_id;
                END IF;

                IF EXTRACT(MONTH FROM rec_date) BETWEEN 1 AND 6 THEN
                    sem_val := EXTRACT(YEAR FROM rec_date)::INT || '_spring';
                ELSE
                    sem_val := EXTRACT(YEAR FROM rec_date)::INT || '_fall';
                END IF;

                NEW.semester := sem_val;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;
            
            DROP TRIGGER IF EXISTS attendance_set_semester ON Attendance;
            CREATE TRIGGER attendance_set_semester
            BEFORE INSERT OR UPDATE OF schedule_id
            ON Attendance
            FOR EACH ROW
            EXECUTE FUNCTION trg_set_attendance_semester();

            -- Partition management function
            CREATE OR REPLACE FUNCTION ensure_attendance_partition(sem TEXT) RETURNS VOID AS $$
            BEGIN
                EXECUTE format(
                    'CREATE TABLE IF NOT EXISTS %I PARTITION OF Attendance FOR VALUES IN (%L)',
                    'Attendance_' || sem,
                    sem
                );
            END;
            $$ LANGUAGE plpgsql;
        ";

        try
        {
            cmd.ExecuteNonQuery();
            Console.WriteLine("PostgreSQL schema created successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating schema: {ex.Message}");
            throw;
        }
    }
}
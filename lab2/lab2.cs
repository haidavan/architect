// Models/ClassroomRequirementReport.cs
using System.ComponentModel.DataAnnotations;

namespace UniversityApi.Models
{
    public class ClassroomRequirementReport
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public int LectureId { get; set; }
        public string LectureName { get; set; }
        public string TechnicalRequirements { get; set; }
        public int StudentCount { get; set; }
    }
}

// Models/ClassroomReportRequest.cs
namespace UniversityApi.Models
{
    public class ClassroomReportRequest
    {
        [Required]
        public string Semester { get; set; } // Формат: "2023_spring"

        [Required]
        public int Year { get; set; }
    }
}
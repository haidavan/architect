// Models/GroupReport.cs
using System.ComponentModel.DataAnnotations;

namespace UniversityApi.Models
{
    public class GroupReport
    {
        public GroupInfo GroupInfo { get; set; }
        public StudentInfo StudentInfo { get; set; }
        public int PlannedHours { get; set; }
        public int AttendedHours { get; set; }
    }

    public class GroupInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DepartmentInfo Department { get; set; }
    }

    public class DepartmentInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class StudentInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class GroupReportRequest
    {
        [Required(ErrorMessage = "Group ID is required")]
        [Range(1, int.MaxValue, ErrorMessage = "Group ID must be a positive number")]
        public int GroupId { get; set; }
    }
}
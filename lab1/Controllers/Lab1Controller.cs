using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using UniversityApi.Services;

namespace UniversityApi.Controllers
{
    [Route("api/lab1")]
    [ApiController]
    public class Lab1Controller : ControllerBase
    {
        private readonly ILogger<Lab1Controller> _logger;
        private readonly IConfiguration _config;
        private readonly IElasticsearchService _elasticsearchService;
        private readonly IAttendanceService _attendanceService;
        private readonly IRedisService _redisService;

        public Lab1Controller(
            ILogger<Lab1Controller> logger,
            IConfiguration config,
            IElasticsearchService elasticsearchService,
            IAttendanceService attendanceService,
            IRedisService redisService)
        {
            _logger = logger;
            _config = config;
            _elasticsearchService = elasticsearchService;
            _attendanceService = attendanceService;
            _redisService = redisService;
        }

        [HttpPost("report")]
        public async Task<IActionResult> GenerateAttendanceReport([FromBody] ReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    error = "Invalid request data",
                    details = ModelState.Values.SelectMany(v => v.Errors)
                });
            }

            try
            {
                // Поиск лекций по ключевому термину
                var lectureIds = await _elasticsearchService.SearchLectures(request.Term);
                if (!lectureIds.Any())
                {
                    return NotFound(new { error = "No lectures found for the term" });
                }

                // Получение студентов с худшей посещаемостью
                var worstAttendees = await _attendanceService.FindWorstAttendees(
                    lectureIds,
                    request.StartDate,
                    request.EndDate,
                    topN: 10
                );

                // Формирование отчета
                var report = new ReportResponse
                {
                    SearchTerm = request.Term,
                    Period = $"{request.StartDate:yyyy-MM-dd} - {request.EndDate:yyyy-MM-dd}",
                    FoundLectures = lectureIds.Count,
                    WorstAttendees = new List<AttendeeInfo>()
                };

                foreach (var attendee in worstAttendees)
                {
                    var redisInfo = await _redisService.GetStudentInfo(attendee.StudentId);
                    report.WorstAttendees.Add(new AttendeeInfo
                    {
                        StudentId = attendee.StudentId,
                        AttendanceCount = attendee.AttendanceCount,
                        RedisInfo = redisInfo
                    });
                }

                return Ok(new
                {
                    report,
                    meta = new
                    {
                        status = "success",
                        results = worstAttendees.Count
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating attendance report");
                return StatusCode(500, new { error = "Data processing failed" });
            }
        }
    }

    public class ReportRequest
    {
        [Required(ErrorMessage = "Search term is required")]
        public string Term { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
    }

    public class ReportResponse
    {
        public string SearchTerm { get; set; }
        public string Period { get; set; }
        public int FoundLectures { get; set; }
        public List<AttendeeInfo> WorstAttendees { get; set; }
    }

    public class AttendeeInfo
    {
        public int StudentId { get; set; }
        public int AttendanceCount { get; set; }
        public StudentRedisInfo RedisInfo { get; set; }
    }
}
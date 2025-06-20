// Controllers/AudienceReportController.cs
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using UniversityApi.Services;

namespace UniversityApi.Controllers
{
    [ApiController]
    [Route("api/lab2")]
    public class AudienceReportController : ControllerBase
    {
        private readonly INeo4jService _neo4jService;
        private readonly ILogger<AudienceReportController> _logger;

        public AudienceReportController(
            INeo4jService neo4jService,
            ILogger<AudienceReportController> logger)
        {
            _neo4jService = neo4jService;
            _logger = logger;
        }

        [HttpPost("audience_report")]
        public async Task<IActionResult> GenerateReport([FromBody] AudienceReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                _logger.LogInformation($"Generating audience report for year: {request.Year}, semester: {request.Semester}");
                var report = await _neo4jService.GenerateAudienceReport(request.Year, request.Semester);

                return Ok(new
                {
                    status = "success",
                    results = report.Count,
                    year = request.Year,
                    semester = request.Semester,
                    data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating audience report: {ex.Message}");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Internal server error",
                    details = ex.Message
                });
            }
        }
    }

    public class AudienceReportRequest
    {
        [Required(ErrorMessage = "Year is required")]
        [Range(2000, 2100, ErrorMessage = "Year must be between 2000 and 2100")]
        public int Year { get; set; }

        [Required(ErrorMessage = "Semester is required")]
        [Range(1, 2, ErrorMessage = "Semester must be 1 or 2")]
        public int Semester { get; set; }
    }
}
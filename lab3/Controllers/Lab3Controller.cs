// Controllers/GroupReportController.cs
using Microsoft.AspNetCore.Mvc;
using UniversityApi.Models;
using UniversityApi.Services;

namespace UniversityApi.Controllers
{
    [ApiController]
    [Route("api/lab3")]
    public class GroupReportController : ControllerBase
    {
        private readonly IGroupReportService _reportService;
        private readonly ILogger<GroupReportController> _logger;

        public GroupReportController(
            IGroupReportService reportService,
            ILogger<GroupReportController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpPost("group_report")]
        public async Task<IActionResult> GenerateReport([FromBody] GroupReportRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                _logger.LogInformation($"Generating group report for group ID: {request.GroupId}");
                var report = await _reportService.GenerateGroupReport(request.GroupId);

                return Ok(new
                {
                    status = "success",
                    groupId = request.GroupId,
                    count = report.Count,
                    data = report
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating group report: {ex.Message}");
                return StatusCode(500, new
                {
                    status = "error",
                    message = "Internal server error",
                    details = ex.Message
                });
            }
        }
    }
}
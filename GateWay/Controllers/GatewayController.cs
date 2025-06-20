using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Добавьте это
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GatewayAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/gateway")]
    public class GatewayController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<GatewayController> _logger; // Добавьте поле

        public GatewayController(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<GatewayController> logger) // Добавьте параметр
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger; // Инициализируйте поле
        }

        [HttpPost("lab1")]
        public async Task<IActionResult> ProxyToLab1([FromBody] object request)
        {
            return await ProxyRequest("Lab1", "api/lab1/report", request);
        }

        [HttpPost("lab2")]
        public async Task<IActionResult> ProxyToLab2([FromBody] object request)
        {
            return await ProxyRequest("Lab2", "api/lab2/audience_report", request);
        }

        [HttpPost("lab3")]
        public async Task<IActionResult> ProxyToLab3([FromBody] object request)
        {
            return await ProxyRequest("Lab3", "api/lab3/group_report", request);
        }

        private async Task<IActionResult> ProxyRequest(string labName, string endpoint, object request)
        {
            var baseUrl = _config[$"{labName}:BaseUrl"];
            var client = _httpClientFactory.CreateClient();

            // Добавим логирование
            _logger.LogInformation($"Proxying to {baseUrl}/{endpoint}");

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            try
            {
                var response = await client.PostAsync($"{baseUrl}/{endpoint}", content);

                // Логируем статус ответа
                _logger.LogInformation($"Response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    return Content(responseContent, "application/json");
                }

                // Логируем тело ошибки
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error response: {errorContent}");

                return StatusCode((int)response.StatusCode, errorContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Proxy request failed to {baseUrl}/{endpoint}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
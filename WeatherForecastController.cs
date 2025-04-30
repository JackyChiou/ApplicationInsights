using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Abstractions;
using System.Net.Http;
using System.Threading.Tasks;

namespace myAPI2.Controllers
{
    public static class Extensions
    {
        public static string GetString<K, V>(this IDictionary<K, V> dict)
        {
            var items = dict.Select(kvp => $"{kvp.Key}:{kvp.Value}");
            return "{" + string.Join(", ", items) + "}";
        }
    }

    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly TelemetryClient _telemetryClient;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, TelemetryClient telemetryClient)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
        }

        [HttpGet]
        public async Task<IEnumerable<WeatherForecast>> Get() // Changed method to async Task<IEnumerable<WeatherForecast>>
        {
            await GetExternalWeather(); // Added 'await' to fix CS4014

            return Enumerable.Range(6, 10).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(56, 75),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("external-weather")]
        public async Task<IActionResult> GetExternalWeather()
        {
            using (var httpClient = new HttpClient())
            {
                string apiUrl = "https://myapp1-eqfparcza9h9f0fb.b01.azurefd.net/weatherforecast";

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);

                    // Add custom headers
                    string customHeader1Value = "Demo1";
                    string customHeader2Value = "Header2";
                    request.Headers.Add("CustomHeader1", customHeader1Value);
                    request.Headers.Add("CustomHeader2", customHeader2Value);

                    // Start telemetry tracking
                    var startTime = DateTime.UtcNow;
                    var timer = System.Diagnostics.Stopwatch.StartNew();

                    var response = await httpClient.SendAsync(request);

                    timer.Stop();
                    var duration = timer.Elapsed;

                    // Log dependency with custom properties
                    var telemetryProperties = new Dictionary<string, string>
                    {
                        { "CustomHeader1", customHeader1Value },
                        { "CustomHeader2", customHeader2Value },
                        { "RequestUrl", apiUrl }
                    };

                    var telemetryMetrics = new Dictionary<string, double>
                    {
                        { "ResponseTimeMs", duration.TotalMilliseconds }
                    };

                    _telemetryClient.TrackDependency(
                        dependencyTypeName: "HTTP",
                        target: apiUrl,
                        dependencyName: "External Weather API",
                        data: telemetryProperties.GetString(),
                        startTime: startTime,
                        duration: duration,
                        resultCode: response.StatusCode.ToString(), // Added resultCode parameter
                        success: response.IsSuccessStatusCode
                    );

                    // Add custom properties to telemetry
                    _telemetryClient.TrackTrace("ExternalWeatherRequest", Microsoft.ApplicationInsights.DataContracts.SeverityLevel.Warning, telemetryProperties);
                    _telemetryClient.TrackEvent("ExternalWeatherRequest", telemetryProperties, telemetryMetrics);
                    _telemetryClient.Flush();

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        return Ok(content); // Return the response content
                    }
                    else
                    {
                        return StatusCode((int)response.StatusCode, "Error fetching data from external API");
                    }
                }
                catch (Exception ex)
                {
                    // Log exception with custom properties
                    _telemetryClient.TrackException(ex, new Dictionary<string, string>
                    {
                        { "CustomHeader1", "Demo1" },
                        { "CustomHeader2", "Header2" },
                        { "RequestUrl", apiUrl }
                    });
                    _telemetryClient.Flush();

                    return StatusCode(500, $"Internal server error: {ex.Message}");
                }
            }
        }
    }
}

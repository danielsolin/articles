using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace DS.Articles.AsyncSyncPlugin.AzureFunction
{
    public class AsyncFunction
    {
        private readonly ILogger<AsyncFunction> _logger;
        private readonly HttpClient _httpClient;

        public AsyncFunction(ILogger<AsyncFunction> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        [Function("AsyncFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] 
            HttpRequest req)
        {
            _logger.LogInformation("Processing request in AsyncFunction.");

            string body;
            try
            {
                body = await new StreamReader(req.Body).ReadToEndAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read request body.");
                return new BadRequestObjectResult("Invalid request body.");
            }

            JObject parsed;
            try
            {
                parsed = JObject.Parse(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON.");
                return new BadRequestObjectResult("Invalid JSON format.");
            }

            var urls = parsed["urls"]?.ToObject<string[]>() ?? Array.Empty<string>();

            if (!urls.Any())
            {
                _logger.LogWarning("No URLs provided in the request.");
                return new BadRequestObjectResult("No URLs provided.");
            }

            var tasks = urls.Select(async url =>
            {
                try
                {
                    _logger.LogInformation($"Fetching data from URL: {url}");
                    _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DS-Agent/1.0");
                    return await _httpClient.GetStringAsync(url);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to fetch data from URL: {url}");
                    return $"Error fetching data from {url}: {ex.Message}";
                }
            });

            string[] results;
            try
            {
                results = await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during parallel execution of HTTP requests.");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }

            var response = new
            {
                results
            };

            _logger.LogInformation("Successfully processed all URLs.");
            return new OkObjectResult(response);
        }
    }
}

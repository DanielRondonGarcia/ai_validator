using AI.Library.Models;
using AI.Library.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AI.Library.Providers.AI
{
    public class GeminiAnalysisAdapter : IAiAnalysisProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiAnalysisAdapter> _logger;
        private readonly AIModelsConfiguration _aiConfig;

        public GeminiAnalysisAdapter(HttpClient httpClient, ILogger<GeminiAnalysisAdapter> logger, IOptions<AIModelsConfiguration> aiConfig)
        {
            _httpClient = httpClient;
            _logger = logger;
            _aiConfig = aiConfig.Value;
        }

        public string ProviderName => "Google";

        public async Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Get configuration from appsettings
            var config = _aiConfig.AnalysisModel;
            var model = config.Model;
            var apiKey = config.ApiKey;
            var baseUrl = config.BaseUrl;
            var url = $"{baseUrl}/models/{model}:generateContent?key={apiKey}";

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var analysisText = jsonResponse.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

                return new ValidationAnalysisResult
                {
                    Success = true,
                    Analysis = analysisText,
                    ModelUsed = config.Model,
                    Provider = ProviderName
                };
            }
            else
            {
                _logger.LogError("Gemini API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Gemini API error: {response.StatusCode}",
                    ModelUsed = config.Model,
                    Provider = ProviderName
                };
            }
        }
    }
}

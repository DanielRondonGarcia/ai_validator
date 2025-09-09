using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataValidator.Infrastructure.Providers.AI
{
    public class GeminiAnalysisAdapter : IAiAnalysisProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiAnalysisAdapter> _logger;

        public GeminiAnalysisAdapter(HttpClient httpClient, ILogger<GeminiAnalysisAdapter> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
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

            // The API key and model name should be injected via config
            var model = "gemini-pro";
            var apiKey = "YOUR_API_KEY";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

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
                    ModelUsed = model,
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
                    ModelUsed = model,
                    Provider = ProviderName
                };
            }
        }
    }
}

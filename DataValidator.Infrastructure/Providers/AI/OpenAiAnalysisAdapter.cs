using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataValidator.Infrastructure.Providers.AI
{
    public class OpenAiAnalysisAdapter : IAiAnalysisProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiAnalysisAdapter> _logger;

        public OpenAiAnalysisAdapter(HttpClient httpClient, ILogger<OpenAiAnalysisAdapter> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string ProviderName => "OpenAI";

        public async Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt)
        {
            var requestBody = new
            {
                model = "gpt-4-turbo-preview", // This should come from config
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert data validation analyst. Analyze the provided data carefully and provide detailed, accurate validation results in the specified JSON format."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 4096, // This should come from config
                temperature = 0.2, // This should come from config
                response_format = new { type = "json_object" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // API key should be handled by HttpClientFactory
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var analysisText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                return new ValidationAnalysisResult
                {
                    Success = true,
                    Analysis = analysisText,
                    ModelUsed = "gpt-4-turbo-preview",
                    Provider = ProviderName
                };
            }
            else
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"OpenAI API error: {response.StatusCode}",
                    ModelUsed = "gpt-4-turbo-preview",
                    Provider = ProviderName
                };
            }
        }
    }
}

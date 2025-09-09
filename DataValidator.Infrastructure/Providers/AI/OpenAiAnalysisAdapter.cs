using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly AIModelsConfiguration _aiConfig;

        public OpenAiAnalysisAdapter(HttpClient httpClient, ILogger<OpenAiAnalysisAdapter> logger, IOptions<AIModelsConfiguration> aiConfig)
        {
            _httpClient = httpClient;
            _logger = logger;
            _aiConfig = aiConfig.Value;
        }

        public string ProviderName => "OpenAI";

        public async Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt)
        {
            // Get configuration from appsettings
            var config = _aiConfig.AnalysisModel;
            
            var requestBody = new
            {
                model = config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"You are an expert data validation analyst with extensive experience in document processing and data verification. Current date: {DateTime.Now:yyyy-MM-dd HH:mm:ss UTC}. \n\nYour responsibilities:\n- Analyze provided data with meticulous attention to detail\n- Identify inconsistencies, errors, or missing information\n- Validate data formats, ranges, and business logic\n- Consider temporal context when evaluating date-sensitive information\n- Provide comprehensive validation results in the specified JSON format\n- Flag any anomalies or suspicious patterns\n\nAlways maintain objectivity and provide clear reasoning for your validation decisions."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = config.MaxTokens,
                temperature = config.Temperature,
                response_format = new { type = "json_object" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Set authorization header with API key from configuration
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
            
            var response = await _httpClient.PostAsync($"{config.BaseUrl}/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var analysisText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

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
                _logger.LogError("OpenAI API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"OpenAI API error: {response.StatusCode}",
                    ModelUsed = config.Model,
                    Provider = ProviderName
                };
            }
        }
    }
}

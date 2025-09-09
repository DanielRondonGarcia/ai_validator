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
    public class OpenAiVisionAdapter : IAiVisionProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<OpenAiVisionAdapter> _logger;
        private readonly AIModelsConfiguration _aiConfig;

        public OpenAiVisionAdapter(HttpClient httpClient, ILogger<OpenAiVisionAdapter> logger, IOptions<AIModelsConfiguration> aiConfig)
        {
            _httpClient = httpClient;
            _logger = logger;
            _aiConfig = aiConfig.Value;
        }

        public string ProviderName => "OpenAI";

        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt)
        {
            // Get configuration from appsettings
            var config = _aiConfig.VisionModel;
            
            var base64Image = System.Convert.ToBase64String(imageData);

            var requestBody = new
            {
                model = config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:{mimeType};base64,{base64Image}"
                                }
                            }
                        }
                    }
                },
                max_tokens = config.MaxTokens,
                temperature = config.Temperature
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
                var extractedText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                return new VisionExtractionResult
                {
                    Success = true,
                    ExtractedData = extractedText,
                    ModelUsed = config.Model,
                    Provider = ProviderName
                };
            }
            else
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new VisionExtractionResult
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

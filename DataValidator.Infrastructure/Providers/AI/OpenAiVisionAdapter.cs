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
            
            _logger.LogInformation("Processing image data - Size: {ImageSize} bytes, MIME Type: {MimeType}", imageData.Length, mimeType);
            
            // Validate image data
            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogError("Invalid image data: null or empty");
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = "Invalid image data: null or empty",
                    ModelUsed = config.Model,
                    Provider = ProviderName
                };
            }
            
            // Validate MIME type
            if (string.IsNullOrEmpty(mimeType) || !mimeType.StartsWith("image/"))
            {
                _logger.LogError("Invalid MIME type: {MimeType}. Expected image/* format", mimeType);
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Invalid MIME type: {mimeType}. Expected image/* format",
                    ModelUsed = config.Model,
                    Provider = ProviderName
                };
            }
            
            var base64Image = System.Convert.ToBase64String(imageData);
            _logger.LogInformation("Base64 image length: {Base64Length} characters", base64Image.Length);

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
                
                _logger.LogInformation("OpenAI Vision response received. Content length: {ContentLength}", extractedText.Length);
                _logger.LogInformation("OpenAI Vision extracted content: {ExtractedContent}", extractedText);

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

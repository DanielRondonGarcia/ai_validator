using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataValidator.Infrastructure.Providers.AI
{
    public class GeminiVisionAdapter : IAiVisionProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GeminiVisionAdapter> _logger;

        public GeminiVisionAdapter(HttpClient httpClient, ILogger<GeminiVisionAdapter> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string ProviderName => "Google";

        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt)
        {
            var base64Image = System.Convert.ToBase64String(imageData);

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = prompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // The API key and model name should be injected via config
            var model = "gemini-pro-vision";
            var apiKey = "YOUR_API_KEY";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var extractedText = jsonResponse.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

                return new VisionExtractionResult
                {
                    Success = true,
                    ExtractedData = extractedText,
                    ModelUsed = model,
                    Provider = ProviderName
                };
            }
            else
            {
                _logger.LogError("Gemini API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new VisionExtractionResult
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

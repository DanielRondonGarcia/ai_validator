using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
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

        public OpenAiVisionAdapter(HttpClient httpClient, ILogger<OpenAiVisionAdapter> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public string ProviderName => "OpenAI";

        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt)
        {
            // This method will now require the full config to be passed in,
            // or we need a factory to configure the HttpClient per provider.
            // For now, let's assume the API key and other configs are handled elsewhere (e.g., HttpClient middleware or factory)

            var base64Image = System.Convert.ToBase64String(imageData);

            var requestBody = new
            {
                model = "gpt-4-vision-preview", // This should come from config
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
                max_tokens = 4096 // This should come from config
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // The API key should be injected securely, perhaps via HttpClientFactory configuration
            // _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "YOUR_API_KEY");

            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var extractedText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                return new VisionExtractionResult
                {
                    Success = true,
                    ExtractedData = extractedText,
                    ModelUsed = "gpt-4-vision-preview",
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
                    ModelUsed = "gpt-4-vision-preview",
                    Provider = ProviderName
                };
            }
        }
    }
}

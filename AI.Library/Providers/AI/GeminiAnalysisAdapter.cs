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
    /// <summary>
    /// Adaptador para el proveedor Google Gemini que implementa servicios de análisis y visión
    /// </summary>
    public class GeminiAnalysisAdapter : IAiAnalysisProvider, IAiVisionProvider
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

        /// <summary>
        /// Extrae datos de una imagen utilizando Google Gemini Vision
        /// </summary>
        /// <param name="imageData">Datos binarios de la imagen</param>
        /// <param name="mimeType">Tipo MIME de la imagen</param>
        /// <param name="prompt">Prompt para guiar la extracción</param>
        /// <returns>Resultado de la extracción</returns>
        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt)
        {
            var startTime = DateTime.UtcNow;
            try
            {
                _logger.LogInformation("Iniciando extracción de datos de imagen con Gemini Vision");

                var base64Image = Convert.ToBase64String(imageData);
                var config = _aiConfig.VisionModel ?? _aiConfig.AnalysisModel;

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

                var url = $"{config.BaseUrl}?key={config.ApiKey}";
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                var processingTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonDocument.Parse(responseContent);
                    var extractedText = jsonResponse.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString() ?? string.Empty;

                    _logger.LogInformation("Extracción de imagen completada exitosamente");
                    return new VisionExtractionResult
                    {
                        Success = true,
                        ExtractedData = extractedText,
                        Metadata = new Dictionary<string, object>
                        {
                            ["model"] = config.Model,
                            ["provider"] = ProviderName,
                            ["processingTimeMs"] = processingTime,
                            ["imageSize"] = imageData.Length,
                            ["mimeType"] = mimeType
                        }
                    };
                }
                else
                {
                    _logger.LogError("Gemini Vision API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                    return new VisionExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"Gemini Vision API error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la extracción de datos de imagen");
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Error durante la extracción: {ex.Message}"
                };
            }
        }
    }
}

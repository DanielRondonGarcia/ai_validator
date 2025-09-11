using AI.Library.Models;
using AI.Library.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AI.Library.Providers.AI
{
    /// <summary>
    /// Adaptador para el proveedor OpenAI que implementa servicios de análisis y visión
    /// </summary>
    public class OpenAiAnalysisAdapter : IAiAnalysisProvider, IAiVisionProvider
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
                        content = $"Eres un analista experto en validación de datos con amplia experiencia en procesamiento de documentos y verificación de datos. Fecha actual: {DateTime.Now:yyyy-MM-dd HH:mm:ss UTC}. \n\nTus responsabilidades:\n- Analizar los datos proporcionados con meticulosa atención al detalle\n- Identificar inconsistencias, errores o información faltante\n- Validar formatos de datos, rangos y lógica de negocio\n- Considerar el contexto temporal al evaluar información sensible a fechas\n- Proporcionar resultados de validación completos en el formato JSON especificado\n- Señalar cualquier anomalía o patrón sospechoso\n\nMantén siempre la objetividad y proporciona un razonamiento claro para tus decisiones de validación."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                //max_tokens = config.MaxTokens,
                max_completion_tokens = config.MaxTokens,
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

        /// <summary>
        /// Extrae datos de una imagen utilizando OpenAI Vision
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
                _logger.LogInformation("Iniciando extracción de datos de imagen con OpenAI Vision");

                var base64Image = Convert.ToBase64String(imageData);
                var config = _aiConfig.VisionModel ?? _aiConfig.AnalysisModel;

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

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

                var response = await _httpClient.PostAsync(config.BaseUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();
                var processingTime = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = JsonDocument.Parse(responseContent);
                    var extractedText = jsonResponse.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
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
                    _logger.LogError("OpenAI Vision API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                    return new VisionExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"OpenAI Vision API error: {response.StatusCode}"
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

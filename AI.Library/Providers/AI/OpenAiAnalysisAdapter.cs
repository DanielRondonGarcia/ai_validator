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
    }
}

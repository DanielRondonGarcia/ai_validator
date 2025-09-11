using AI.Library.Models;
using AI.Library.Ports;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI.Library.Core
{
    /// <summary>
    /// Servicio principal que proporciona todas las funcionalidades de IA de la librería
    /// </summary>
    public class AIService : IAIService
    {
        private readonly IAiVisionProvider _visionProvider;
        private readonly IAiAnalysisProvider _analysisProvider;
        private readonly IPdfProcessor _pdfProcessor;
        private readonly ILogger<AIService> _logger;

        public AIService(
            IAiVisionProvider visionProvider,
            IAiAnalysisProvider analysisProvider,
            IPdfProcessor pdfProcessor,
            ILogger<AIService> logger)
        {
            _visionProvider = visionProvider;
            _analysisProvider = analysisProvider;
            _pdfProcessor = pdfProcessor;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt)
        {
            try
            {
                _logger.LogInformation("Iniciando extracción de datos de imagen");
                var result = await _visionProvider.ExtractDataFromImageAsync(imageData, mimeType, prompt);
                _logger.LogInformation("Extracción de imagen completada. Éxito: {Success}", result.Success);
                return result;
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

        /// <inheritdoc/>
        public async Task<VisionExtractionResult> ExtractDataFromPdfAsync(byte[] pdfData, string prompt)
        {
            try
            {
                _logger.LogInformation("Iniciando extracción de datos de PDF");
                
                // Convertir PDF a imágenes
                var images = await _pdfProcessor.ConvertPdfPagesToImagesAsync(pdfData);
                if (!images.Any())
                {
                    return new VisionExtractionResult
                    {
                        Success = false,
                        ErrorMessage = "No se pudieron extraer páginas del PDF"
                    };
                }

                // Procesar cada página y combinar resultados
                var allExtractedData = new List<object>();
                var allMetadata = new Dictionary<string, object>();

                foreach (var (imageData, pageNumber) in images)
                {
                    var pageResult = await _visionProvider.ExtractDataFromImageAsync(imageData, "image/png", prompt);
                    if (pageResult.Success && !string.IsNullOrEmpty(pageResult.ExtractedData))
                    {
                        allExtractedData.Add(new { Page = pageNumber, Data = pageResult.ExtractedData });
                        if (pageResult.Metadata != null)
                        {
                            allMetadata[$"Page_{pageNumber}"] = pageResult.Metadata;
                        }
                    }
                }

                var combinedResult = new VisionExtractionResult
                {
                    Success = allExtractedData.Any(),
                    ExtractedData = JsonSerializer.Serialize(allExtractedData, new JsonSerializerOptions { WriteIndented = true }),
                    Metadata = allMetadata
                };

                _logger.LogInformation("Extracción de PDF completada. Páginas procesadas: {PageCount}", images.Count);
                return combinedResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la extracción de datos de PDF");
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Error durante la extracción de PDF: {ex.Message}"
                };
            }
        }

        /// <inheritdoc/>
        public async Task<ValidationAnalysisResult> ValidateDataAsync(string extractedData, string referenceJson, string[]? validationRules = null)
        {
            try
            {
                _logger.LogInformation("Iniciando validación de datos");
                
                var prompt = BuildValidationPrompt(extractedData, referenceJson, validationRules);
                var result = await _analysisProvider.AnalyzeDataAsync(prompt);
                
                _logger.LogInformation("Validación completada. Válido: {IsValid}", result.IsValid);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la validación de datos");
                return new ValidationAnalysisResult
                {
                    Success = false,
                    IsValid = false,
                    Discrepancies = new List<ValidationDiscrepancy> { new ValidationDiscrepancy { Field = "general", Description = $"Error durante la validación: {ex.Message}", DiscrepancyType = "error" } }
                };
            }
        }

        /// <inheritdoc/>
        public async Task<TranslationResult> TranslateTextAsync(string text, string targetLanguage, string? sourceLanguage = null)
        {
            try
            {
                _logger.LogInformation("Iniciando traducción de texto a {TargetLanguage}", targetLanguage);
                
                var prompt = BuildTranslationPrompt(text, targetLanguage, sourceLanguage);
                var analysisResult = await _analysisProvider.AnalyzeDataAsync(prompt);
                
                if (!analysisResult.Success)
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "Error en el servicio de análisis durante la traducción"
                    };
                }

                // Parsear el resultado de la traducción
                var translationResult = ParseTranslationResult(analysisResult, targetLanguage);
                
                _logger.LogInformation("Traducción completada. Éxito: {Success}", translationResult.Success);
                return translationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la traducción");
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = $"Error durante la traducción: {ex.Message}"
                };
            }
        }

        private string BuildValidationPrompt(string extractedData, string referenceJson, string[]? validationRules)
        {
            var prompt = $@"
Por favor, valida los siguientes datos extraídos contra el JSON de referencia:

**Datos Extraídos:**
{extractedData}

**JSON de Referencia:**
{referenceJson}
";

            if (validationRules?.Any() == true)
            {
                prompt += $"\n**Reglas de Validación Específicas:**\n{string.Join("\n", validationRules.Select((rule, i) => $"{i + 1}. {rule}"))}";
            }

            prompt += @"

Proporciona un análisis detallado en formato JSON con la siguiente estructura:
{
  ""success"": true/false,
  ""isValid"": true/false,
  ""discrepancies"": [""lista de discrepancias encontradas""],
  ""confidenceScore"": 0.0-1.0,
  ""processingTime"": tiempo_en_ms
}";

            return prompt;
        }

        private string BuildTranslationPrompt(string text, string targetLanguage, string? sourceLanguage)
        {
            var prompt = $@"
Traduce el siguiente texto al {targetLanguage}";
            
            if (!string.IsNullOrEmpty(sourceLanguage))
            {
                prompt += $" desde {sourceLanguage}";
            }
            
            prompt += $@":

**Texto a traducir:**
{text}

Proporciona el resultado en formato JSON con la siguiente estructura:
{{
  ""success"": true,
  ""translatedText"": ""texto traducido"",
  ""detectedSourceLanguage"": ""idioma detectado"",
  ""targetLanguage"": ""{targetLanguage}"",
  ""confidenceScore"": 0.0-1.0
}}";

            return prompt;
        }

        private TranslationResult ParseTranslationResult(ValidationAnalysisResult analysisResult, string targetLanguage)
        {
            try
            {
                // Intentar parsear el resultado como JSON
                var jsonData = analysisResult.Discrepancies?.FirstOrDefault()?.Description ?? "{}";
                var jsonDoc = JsonDocument.Parse(jsonData);
                var root = jsonDoc.RootElement;

                return new TranslationResult
                {
                    Success = root.TryGetProperty("success", out var successProp) && successProp.GetBoolean(),
                    TranslatedText = root.TryGetProperty("translatedText", out var textProp) ? textProp.GetString() ?? "" : "",
                    DetectedSourceLanguage = root.TryGetProperty("detectedSourceLanguage", out var sourceProp) ? sourceProp.GetString() ?? "" : "",
                    TargetLanguage = targetLanguage,
                    ConfidenceScore = root.TryGetProperty("confidenceScore", out var confProp) ? confProp.GetDouble() : 0.0,
                    ProcessingTimeMs = (long)analysisResult.ProcessingTime.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parseando resultado de traducción, usando fallback");
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = "Error parseando el resultado de la traducción",
                    TargetLanguage = targetLanguage
                };
            }
        }
    }
}
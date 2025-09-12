using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using DataValidator.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DataValidator.API.Services
{
    public class AnalysisValidationService : IAnalysisValidationService
    {
        private readonly ILogger<AnalysisValidationService> _logger;
        private readonly IPromptBuilderService _promptBuilder;
        private readonly IEnumerable<IAiAnalysisProvider> _analysisProviders;
        private readonly AIModelsConfiguration _aiConfig;

        public AnalysisValidationService(
            ILogger<AnalysisValidationService> logger,
            IPromptBuilderService promptBuilder,
            IEnumerable<IAiAnalysisProvider> analysisProviders,
            IOptions<AIModelsConfiguration> aiConfig)
        {
            _logger = logger;
            _promptBuilder = promptBuilder;
            _analysisProviders = analysisProviders;
            _aiConfig = aiConfig.Value;
        }

        public async Task<ValidationAnalysisResult> ValidateExtractedDataAsync(string extractedData, string documentType, List<string> fieldsToValidate, string jsonData)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                // Validate input parameters
                if (string.IsNullOrWhiteSpace(extractedData))
                {
                    _logger.LogError("ExtractedData is null or empty");
                    return new ValidationAnalysisResult 
                    { 
                        Success = false, 
                        ErrorMessage = "Los datos extraídos están vacíos. No se puede realizar la validación.",
                        ProcessingTime = stopwatch.Elapsed
                    };
                }

                if (string.IsNullOrWhiteSpace(documentType))
                {
                    _logger.LogError("DocumentType is null or empty");
                    return new ValidationAnalysisResult 
                    { 
                        Success = false, 
                        ErrorMessage = "El tipo de documento no está especificado.",
                        ProcessingTime = stopwatch.Elapsed
                    };
                }

                _logger.LogInformation("Starting validation for document type: {DocumentType} with {FieldCount} fields to validate", 
                    documentType, fieldsToValidate?.Count ?? 0);

                var basePrompt = _promptBuilder.BuildValidationPrompt(documentType, fieldsToValidate, jsonData);
                var finalPrompt = BuildFinalValidationPrompt(extractedData, basePrompt);

                var primaryProvider = _analysisProviders.FirstOrDefault(p => p.ProviderName == _aiConfig.AnalysisModel.Provider);
                if (primaryProvider == null)
                {
                    _logger.LogError("Primary analysis provider '{Provider}' not found. Available providers: {AvailableProviders}", 
                        _aiConfig.AnalysisModel.Provider, 
                        string.Join(", ", _analysisProviders.Select(p => p.ProviderName)));
                    
                    return new ValidationAnalysisResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"El proveedor de IA configurado '{_aiConfig.AnalysisModel.Provider}' no está disponible. Verifique la configuración.",
                        ProcessingTime = stopwatch.Elapsed
                    };
                }

                _logger.LogInformation("Using primary provider: {Provider}", primaryProvider.ProviderName);
                var result = await primaryProvider.AnalyzeDataAsync(finalPrompt);

                // If primary provider fails, try alternative providers
                if (!result.Success)
                {
                    _logger.LogWarning("Primary provider '{Provider}' failed with error: {Error}", 
                        primaryProvider.ProviderName, result.ErrorMessage);
                    
                    var alternativeProviders = _analysisProviders.Where(p => p.ProviderName != _aiConfig.AnalysisModel.Provider).ToList();
                    
                    if (alternativeProviders.Any())
                    {
                        _logger.LogInformation("Attempting to use {Count} alternative providers", alternativeProviders.Count);
                        
                        foreach (var alternativeProvider in alternativeProviders)
                        {
                            _logger.LogInformation("Trying alternative provider: {Provider}", alternativeProvider.ProviderName);
                            
                            try
                            {
                                result = await alternativeProvider.AnalyzeDataAsync(finalPrompt);
                                if (result.Success)
                                {
                                    _logger.LogInformation("Alternative provider '{Provider}' succeeded", alternativeProvider.ProviderName);
                                    break;
                                }
                                else
                                {
                                    _logger.LogWarning("Alternative provider '{Provider}' also failed: {Error}", 
                                        alternativeProvider.ProviderName, result.ErrorMessage);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Exception when trying alternative provider '{Provider}': {Message}", 
                                    alternativeProvider.ProviderName, ex.Message);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No alternative providers available");
                    }

                    // If all providers failed, enhance the error message
                    if (!result.Success)
                    {
                        var enhancedErrorMessage = "No se pudo completar la validación. ";
                        if (alternativeProviders.Any())
                        {
                            enhancedErrorMessage += $"Tanto el proveedor principal ({primaryProvider.ProviderName}) como los alternativos fallaron. ";
                        }
                        else
                        {
                            enhancedErrorMessage += $"El proveedor principal ({primaryProvider.ProviderName}) falló y no hay proveedores alternativos configurados. ";
                        }
                        enhancedErrorMessage += "Verifique la conectividad de red y la configuración de la IA.";
                        
                        result.ErrorMessage = enhancedErrorMessage;
                        _logger.LogError("All AI providers failed. Final error: {Error}", result.ErrorMessage);
                    }
                }

                if (result.Success)
                {
                    _logger.LogInformation("AI analysis completed successfully, parsing response");
                    
                    try
                    {
                        var parsedResult = ParseAnalysisResponse(result.Analysis);
                        
                        if (parsedResult.Success)
                        {
                            // Copy properties from parsed result to the provider result
                            result.IsValid = parsedResult.IsValid;
                            result.Discrepancies = parsedResult.Discrepancies;
                            result.ConfidenceScore = parsedResult.ConfidenceScore;
                            result.Analysis = parsedResult.Analysis; // Keep the parsed analysis text
                            
                            _logger.LogInformation("Validation completed. IsValid: {IsValid}, ConfidenceScore: {ConfidenceScore}, Discrepancies: {DiscrepancyCount}", 
                                result.IsValid, result.ConfidenceScore, result.Discrepancies?.Count ?? 0);
                        }
                        else
                        {
                            // If parsing failed, but we got a response, include both errors
                            result.Success = false;
                            result.ErrorMessage = $"La IA respondió pero el formato de respuesta es inválido: {parsedResult.ErrorMessage}";
                            _logger.LogError("Failed to parse AI response: {Error}", parsedResult.ErrorMessage);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unexpected error while parsing AI response: {Message}", ex.Message);
                        result.Success = false;
                        result.ErrorMessage = $"Error inesperado al procesar la respuesta de la IA: {ex.Message}";
                    }
                }

                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                
                _logger.LogInformation("Validation process completed in {ProcessingTime}ms. Success: {Success}", 
                    stopwatch.ElapsedMilliseconds, result.Success);
                
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Unexpected error during validation process: {Message}", ex.Message);
                
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"Error inesperado durante el proceso de validación: {ex.Message}",
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        public async Task<ValidationAnalysisResult> AnalyzeDiscrepanciesAsync(string extractedData, string documentType, List<string> discrepancies)
        {
            // This method can be refactored similarly if needed.
            // For now, focusing on the main validation path.
            _logger.LogWarning("AnalyzeDiscrepanciesAsync is not fully refactored yet.");
            return await Task.FromResult(new ValidationAnalysisResult { Success = false, ErrorMessage = "Not implemented in refactored service." });
        }

        private string BuildFinalValidationPrompt(string extractedData, string basePrompt)
        {
            // This logic was moved from the old service. It could be moved to the prompt builder as well.
            return $@"
**EXTRACTED DATA FROM DOCUMENT:**
{extractedData}

**VALIDATION INSTRUCTIONS:**
{basePrompt}

**ANALYSIS REQUIREMENTS:**
1. Analyze the extracted data for completeness and accuracy
2. Identify any missing or inconsistent information
3. Consider variations in formatting, spelling, or representation
4. Provide a confidence score (0.0 to 1.0) for the overall validation
5. List specific issues with severity levels

**RESPONSE FORMAT:**
Provide your analysis in the following JSON format:
{{
  ""isValid"": boolean,
  ""confidenceScore"": number,
  ""analysis"": ""detailed analysis text"",
  ""discrepancies"": [
    {{
      ""field"": ""field name"",
      ""extractedValue"": ""value from document"",
      ""providedValue"": ""value from JSON"",
      ""discrepancyType"": ""mismatch|missing|format|other"",
      ""description"": ""detailed description"",
      ""severity"": number (0.0 to 1.0)
    }}
  ]
}}
";
        }

        private ValidationAnalysisResult ParseAnalysisResponse(string analysisJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(analysisJson))
                {
                    _logger.LogError("Analysis JSON is null or empty");
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        IsValid = false,
                        ErrorMessage = "La respuesta de la IA está vacía"
                    };
                }

                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(analysisJson);
                
                // Validate required fields are present
                var missingFields = new List<string>();
                
                if (!jsonDoc.TryGetProperty("isValid", out var isValidProp))
                {
                    missingFields.Add("isValid");
                }
                
                if (!jsonDoc.TryGetProperty("analysis", out var analysisProp))
                {
                    missingFields.Add("analysis");
                }
                
                if (missingFields.Any())
                {
                    _logger.LogError("AI response missing required fields: {MissingFields}. Response: {Response}", 
                        string.Join(", ", missingFields), analysisJson);
                    
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        IsValid = false,
                        Analysis = analysisJson,
                        ErrorMessage = $"La respuesta de la IA no contiene los campos requeridos: {string.Join(", ", missingFields)}"
                    };
                }

                // Validate field types
                if (isValidProp.ValueKind != JsonValueKind.True && isValidProp.ValueKind != JsonValueKind.False)
                {
                    _logger.LogError("Field 'isValid' is not a boolean in AI response: {Response}", analysisJson);
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        IsValid = false,
                        Analysis = analysisJson,
                        ErrorMessage = "El campo 'isValid' debe ser un valor booleano"
                    };
                }

                if (analysisProp.ValueKind != JsonValueKind.String)
                {
                    _logger.LogError("Field 'analysis' is not a string in AI response: {Response}", analysisJson);
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        IsValid = false,
                        Analysis = analysisJson,
                        ErrorMessage = "El campo 'analysis' debe ser una cadena de texto"
                    };
                }

                var result = new ValidationAnalysisResult
                {
                    Success = true,
                    IsValid = isValidProp.GetBoolean(),
                    Analysis = analysisProp.GetString() ?? ""
                };

                // Handle optional confidenceScore field
                if (jsonDoc.TryGetProperty("confidenceScore", out var confidenceProp))
                {
                    if (confidenceProp.ValueKind == JsonValueKind.Number)
                    {
                        var confidence = confidenceProp.GetDouble();
                        if (confidence >= 0.0 && confidence <= 1.0)
                        {
                            result.ConfidenceScore = confidence;
                        }
                        else
                        {
                            _logger.LogWarning("ConfidenceScore {Score} is out of valid range (0.0-1.0), setting to 0.0", confidence);
                            result.ConfidenceScore = 0.0;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("ConfidenceScore is not a number, setting to 0.0");
                        result.ConfidenceScore = 0.0;
                    }
                }
                else
                {
                    result.ConfidenceScore = 0.0;
                }

                // Handle optional discrepancies array
                if (jsonDoc.TryGetProperty("discrepancies", out var discrepanciesProp))
                {
                    if (discrepanciesProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var discrepancy in discrepanciesProp.EnumerateArray())
                        {
                            try
                            {
                                var validationDiscrepancy = new ValidationDiscrepancy
                                {
                                    Field = discrepancy.TryGetProperty("field", out var fieldProp) && fieldProp.ValueKind == JsonValueKind.String 
                                        ? fieldProp.GetString() ?? "" : "",
                                    ExtractedValue = discrepancy.TryGetProperty("extractedValue", out var extractedProp) && extractedProp.ValueKind == JsonValueKind.String 
                                        ? extractedProp.GetString() ?? "" : "",
                                    ProvidedValue = discrepancy.TryGetProperty("providedValue", out var providedProp) && providedProp.ValueKind == JsonValueKind.String 
                                        ? providedProp.GetString() ?? "" : "",
                                    DiscrepancyType = discrepancy.TryGetProperty("discrepancyType", out var typeProp) && typeProp.ValueKind == JsonValueKind.String 
                                        ? typeProp.GetString() ?? "" : "",
                                    Description = discrepancy.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String 
                                        ? descProp.GetString() ?? "" : ""
                                };

                                // Handle severity with validation
                                if (discrepancy.TryGetProperty("severity", out var severityProp) && severityProp.ValueKind == JsonValueKind.Number)
                                {
                                    var severity = severityProp.GetDouble();
                                    validationDiscrepancy.Severity = severity >= 0.0 && severity <= 1.0 ? severity : 0.0;
                                }
                                else
                                {
                                    validationDiscrepancy.Severity = 0.0;
                                }

                                result.Discrepancies.Add(validationDiscrepancy);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error parsing individual discrepancy, skipping: {Message}", ex.Message);
                                // Continue processing other discrepancies
                            }
                        }
                        
                        _logger.LogInformation("Successfully parsed {Count} discrepancies from AI response", result.Discrepancies.Count);
                    }
                    else
                    {
                        _logger.LogWarning("Discrepancies field is not an array, ignoring");
                    }
                }

                _logger.LogInformation("Successfully parsed AI response. IsValid: {IsValid}, ConfidenceScore: {ConfidenceScore}", 
                    result.IsValid, result.ConfidenceScore);
                
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing analysis JSON response: {Response}", analysisJson);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    IsValid = false,
                    Analysis = analysisJson, // Return raw response if parsing fails
                    ErrorMessage = $"Error al analizar la respuesta JSON de la IA: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error parsing analysis response: {Message}", ex.Message);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    IsValid = false,
                    Analysis = analysisJson,
                    ErrorMessage = $"Error inesperado al procesar la respuesta: {ex.Message}"
                };
            }
        }
    }
}

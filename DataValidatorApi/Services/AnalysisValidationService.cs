using DataValidatorApi.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DataValidatorApi.Services
{
    public class AnalysisValidationService : IAnalysisValidationService
    {
        private readonly AIModelsConfiguration _aiConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AnalysisValidationService> _logger;
        private readonly IPromptBuilderService _promptBuilder;

        public AnalysisValidationService(
            IOptions<AIModelsConfiguration> aiConfig,
            HttpClient httpClient,
            ILogger<AnalysisValidationService> logger,
            IPromptBuilderService promptBuilder)
        {
            _aiConfig = aiConfig.Value;
            _httpClient = httpClient;
            _logger = logger;
            _promptBuilder = promptBuilder;
        }

        public async Task<ValidationAnalysisResult> ValidateExtractedDataAsync(string extractedData, string documentType, List<string> fieldsToValidate)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting validation of extracted data for DocumentType: {DocumentType}, ExtractedDataLength: {DataLength}, FieldsToValidate: {FieldsCount}", 
                    documentType, extractedData.Length, fieldsToValidate?.Count ?? 0);
                
                // Build specialized validation prompt
                var validationPrompt = _promptBuilder.BuildValidationPrompt(documentType, fieldsToValidate);
                _logger.LogDebug("Base validation prompt built, Length: {PromptLength}", validationPrompt.Length);
                
                var enhancedPrompt = BuildValidationPrompt(extractedData, validationPrompt);
                _logger.LogDebug("Enhanced validation prompt built, Length: {EnhancedPromptLength}", enhancedPrompt.Length);
                
                // Try primary analysis model first
                _logger.LogInformation("Attempting validation with primary analysis model: {Provider} - {Model}", 
                    _aiConfig.AnalysisModel.Provider, _aiConfig.AnalysisModel.Model);
                var result = await TryAnalyzeWithModel(_aiConfig.AnalysisModel, enhancedPrompt);
                
                if (!result.Success && _aiConfig.AlternativeAnalysisModel.Provider != _aiConfig.AnalysisModel.Provider)
                {
                    _logger.LogWarning("Primary analysis model failed with error: {Error}. Trying alternative model: {Provider} - {Model}", 
                        result.ErrorMessage, _aiConfig.AlternativeAnalysisModel.Provider, _aiConfig.AlternativeAnalysisModel.Model);
                    result = await TryAnalyzeWithModel(_aiConfig.AlternativeAnalysisModel, enhancedPrompt);
                }
                
                if (result.Success)
                {
                    _logger.LogDebug("Analysis successful, parsing response. Analysis length: {AnalysisLength}", result.Analysis?.Length ?? 0);
                    result = await ParseAnalysisResponse(result.Analysis, extractedData, "");
                    _logger.LogInformation("Analysis parsing completed. Issues found: {IssuesCount}", result.Discrepancies?.Count ?? 0);
                }
                
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                result.Metadata["ExtractedDataLength"] = extractedData.Length;
                result.Metadata["DocumentType"] = documentType;
                
                _logger.LogInformation("Validation completed. Success: {Success}, ProcessingTime: {ProcessingTime}ms, Issues: {IssuesCount}", 
                    result.Success, result.ProcessingTime.TotalMilliseconds, result.Discrepancies?.Count ?? 0);
                
                if (!result.Success)
                {
                    _logger.LogError("Validation failed: {ErrorMessage}", result.ErrorMessage);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during validation analysis");
                stopwatch.Stop();
                
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        public async Task<ValidationAnalysisResult> AnalyzeDiscrepanciesAsync(string extractedData, string documentType, List<string> discrepancies)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting discrepancy analysis for DocumentType: {DocumentType}, DiscrepanciesCount: {Count}, ExtractedDataLength: {DataLength}", 
                    documentType, discrepancies.Count, extractedData.Length);
                
                _logger.LogDebug("Discrepancies to analyze: {Discrepancies}", string.Join("; ", discrepancies));
                
                var discrepancyPrompt = _promptBuilder.BuildDiscrepancyAnalysisPrompt(documentType, discrepancies);
                _logger.LogDebug("Base discrepancy prompt built, Length: {PromptLength}", discrepancyPrompt.Length);
                
                var enhancedPrompt = BuildDiscrepancyAnalysisPrompt(extractedData, discrepancyPrompt, discrepancies);
                _logger.LogDebug("Enhanced discrepancy prompt built, Length: {EnhancedPromptLength}", enhancedPrompt.Length);
                
                _logger.LogInformation("Attempting discrepancy analysis with primary model: {Provider} - {Model}", 
                    _aiConfig.AnalysisModel.Provider, _aiConfig.AnalysisModel.Model);
                var result = await TryAnalyzeWithModel(_aiConfig.AnalysisModel, enhancedPrompt);
                
                if (!result.Success && _aiConfig.AlternativeAnalysisModel.Provider != _aiConfig.AnalysisModel.Provider)
                {
                    _logger.LogWarning("Primary analysis model failed with error: {Error}. Trying alternative model: {Provider} - {Model}", 
                        result.ErrorMessage, _aiConfig.AlternativeAnalysisModel.Provider, _aiConfig.AlternativeAnalysisModel.Model);
                    result = await TryAnalyzeWithModel(_aiConfig.AlternativeAnalysisModel, discrepancyPrompt);
                }
                
                if (result.Success)
                {
                    _logger.LogDebug("Discrepancy analysis successful, parsing response. Analysis length: {AnalysisLength}", result.Analysis?.Length ?? 0);
                    result = await ParseDiscrepancyAnalysisResponse(result.Analysis, discrepancies);
                    _logger.LogInformation("Discrepancy analysis parsing completed. Issues found: {IssuesCount}", result.Discrepancies?.Count ?? 0);
                }
                
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                result.Metadata["DiscrepanciesAnalyzed"] = discrepancies.Count;
                
                _logger.LogInformation("Discrepancy analysis completed. Success: {Success}, ProcessingTime: {ProcessingTime}ms, Issues: {IssuesCount}", 
                    result.Success, result.ProcessingTime.TotalMilliseconds, result.Discrepancies?.Count ?? 0);
                
                if (!result.Success)
                {
                    _logger.LogError("Discrepancy analysis failed: {ErrorMessage}", result.ErrorMessage);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during discrepancy analysis");
                stopwatch.Stop();
                
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        private async Task<ValidationAnalysisResult> TryAnalyzeWithModel(AIModelConfig modelConfig, string prompt)
        {
            try
            {
                if (modelConfig.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    return await AnalyzeWithOpenAI(modelConfig, prompt);
                }
                else if (modelConfig.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
                {
                    return await AnalyzeWithGemini(modelConfig, prompt);
                }
                else
                {
                    return new ValidationAnalysisResult
                    {
                        Success = false,
                        ErrorMessage = $"Unsupported provider: {modelConfig.Provider}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error with analysis model {Model} from provider {Provider}", modelConfig.Model, modelConfig.Provider);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ModelUsed = modelConfig.Model,
                    Provider = modelConfig.Provider
                };
            }
        }

        private async Task<ValidationAnalysisResult> AnalyzeWithOpenAI(AIModelConfig config, string prompt)
        {
            var requestBody = new
            {
                model = config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert data validation analyst. Analyze the provided data carefully and provide detailed, accurate validation results in the specified JSON format."
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

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

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
                    Provider = config.Provider
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
                    Provider = config.Provider
                };
            }
        }

        private async Task<ValidationAnalysisResult> AnalyzeWithGemini(AIModelConfig config, string prompt)
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
                },
                generationConfig = new
                {
                    maxOutputTokens = config.MaxTokens,
                    temperature = config.Temperature
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{config.BaseUrl}/models/{config.Model}:generateContent?key={config.ApiKey}";
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
                    Provider = config.Provider
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
                    Provider = config.Provider
                };
            }
        }

        private string BuildValidationPrompt(string extractedData, string basePrompt)
        {
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

        private string BuildDiscrepancyAnalysisPrompt(string extractedData, string basePrompt, List<string> discrepancies)
        {
            var discrepancyList = string.Join("\n- ", discrepancies);
            
            return $@"
**EXTRACTED DATA:**
{extractedData}

**IDENTIFIED DISCREPANCIES:**
- {discrepancyList}

**ANALYSIS INSTRUCTIONS:**
{basePrompt}

**ANALYSIS TASK:**
For each discrepancy, determine:
1. Is it a genuine error or acceptable variation?
2. What is the severity of the discrepancy?
3. What might have caused the discrepancy?
4. Recommendations for resolution
";
        }

        private async Task<ValidationAnalysisResult> ParseAnalysisResponse(string analysisJson, string extractedData, string jsonData)
        {
            try
            {
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(analysisJson);
                
                var result = new ValidationAnalysisResult
                {
                    Success = true,
                    IsValid = jsonDoc.TryGetProperty("isValid", out var isValidProp) && isValidProp.GetBoolean(),
                    ConfidenceScore = jsonDoc.TryGetProperty("confidenceScore", out var confidenceProp) ? confidenceProp.GetDouble() : 0.0,
                    Analysis = jsonDoc.TryGetProperty("analysis", out var analysisProp) ? analysisProp.GetString() ?? "" : ""
                };

                if (jsonDoc.TryGetProperty("discrepancies", out var discrepanciesProp) && discrepanciesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var discrepancy in discrepanciesProp.EnumerateArray())
                    {
                        result.Discrepancies.Add(new ValidationDiscrepancy
                        {
                            Field = discrepancy.TryGetProperty("field", out var fieldProp) ? fieldProp.GetString() ?? "" : "",
                            ExtractedValue = discrepancy.TryGetProperty("extractedValue", out var extractedProp) ? extractedProp.GetString() ?? "" : "",
                            ProvidedValue = discrepancy.TryGetProperty("providedValue", out var providedProp) ? providedProp.GetString() ?? "" : "",
                            DiscrepancyType = discrepancy.TryGetProperty("discrepancyType", out var typeProp) ? typeProp.GetString() ?? "" : "",
                            Description = discrepancy.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "",
                            Severity = discrepancy.TryGetProperty("severity", out var severityProp) ? severityProp.GetDouble() : 0.0
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing analysis response: {Response}", analysisJson);
                
                return new ValidationAnalysisResult
                {
                    Success = true,
                    IsValid = false,
                    Analysis = analysisJson, // Return raw response if parsing fails
                    ConfidenceScore = 0.0,
                    ErrorMessage = $"Failed to parse structured response: {ex.Message}"
                };
            }
        }

        private async Task<ValidationAnalysisResult> ParseDiscrepancyAnalysisResponse(string analysisText, List<string> originalDiscrepancies)
        {
            // For discrepancy analysis, we'll return the raw analysis text
            // In a more sophisticated implementation, you could parse structured responses here too
            
            return new ValidationAnalysisResult
            {
                Success = true,
                Analysis = analysisText,
                IsValid = false, // Discrepancy analysis assumes there are issues
                ConfidenceScore = 0.8, // Default confidence for discrepancy analysis
                Metadata = new Dictionary<string, object>
                {
                    ["OriginalDiscrepancies"] = originalDiscrepancies
                }
            };
        }
    }
}
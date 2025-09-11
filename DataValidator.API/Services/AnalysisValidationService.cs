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
            var basePrompt = _promptBuilder.BuildValidationPrompt(documentType, fieldsToValidate, jsonData);
            var finalPrompt = BuildFinalValidationPrompt(extractedData, basePrompt);

            var primaryProvider = _analysisProviders.FirstOrDefault(p => p.ProviderName == _aiConfig.AnalysisModel.Provider);
            if (primaryProvider == null)
            {
                return new ValidationAnalysisResult { Success = false, ErrorMessage = $"Primary analysis provider '{_aiConfig.AnalysisModel.Provider}' not found." };
            }

            var result = await primaryProvider.AnalyzeDataAsync(finalPrompt);

            // If primary provider fails, try alternative providers
            if (!result.Success)
            {
                var alternativeProviders = _analysisProviders.Where(p => p.ProviderName != _aiConfig.AnalysisModel.Provider);
                foreach (var alternativeProvider in alternativeProviders)
                {
                    _logger.LogWarning("Primary provider failed, trying alternative provider: {Provider}", alternativeProvider.ProviderName);
                    result = await alternativeProvider.AnalyzeDataAsync(finalPrompt);
                    if (result.Success)
                    {
                        break;
                    }
                }
            }

            if (result.Success)
            {
                var parsedResult = ParseAnalysisResponse(result.Analysis);
                // Copy properties from parsed result to the provider result
                result.IsValid = parsedResult.IsValid;
                result.Discrepancies = parsedResult.Discrepancies;
                result.ConfidenceScore = parsedResult.ConfidenceScore;
                result.Analysis = parsedResult.Analysis; // Keep the parsed analysis text
            }

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            return result;
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
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error parsing analysis JSON response: {Response}", analysisJson);
                return new ValidationAnalysisResult
                {
                    Success = false,
                    IsValid = false,
                    Analysis = analysisJson, // Return raw response if parsing fails
                    ErrorMessage = $"Failed to parse AI response: {ex.Message}"
                };
            }
        }
    }
}

using DataValidatorApi.Models;

namespace DataValidatorApi.Services
{
    public interface IAnalysisValidationService
    {
        Task<ValidationAnalysisResult> ValidateExtractedDataAsync(string extractedData, string documentType, List<string> fieldsToValidate);
        Task<ValidationAnalysisResult> AnalyzeDiscrepanciesAsync(string extractedData, string documentType, List<string> discrepancies);
    }

    public class ValidationAnalysisResult
    {
        public bool Success { get; set; }
        public bool IsValid { get; set; }
        public string Analysis { get; set; } = string.Empty;
        public List<ValidationDiscrepancy> Discrepancies { get; set; } = new();
        public double ConfidenceScore { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ValidationDiscrepancy
    {
        public string Field { get; set; } = string.Empty;
        public string ExtractedValue { get; set; } = string.Empty;
        public string ProvidedValue { get; set; } = string.Empty;
        public string DiscrepancyType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Severity { get; set; } // 0.0 to 1.0
    }
}
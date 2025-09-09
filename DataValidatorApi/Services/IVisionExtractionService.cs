using DataValidatorApi.Models;

namespace DataValidatorApi.Services
{
    public interface IVisionExtractionService
    {
        Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string fileName, string documentType, List<string>? fieldsToExtract = null);
    Task<VisionExtractionResult> ExtractDataFromPdfAsync(byte[] pdfData, string fileName, string documentType, List<string>? fieldsToExtract = null);
        Task<bool> IsPdfImageBasedAsync(byte[] pdfData);
    }

    public class VisionExtractionResult
    {
        public bool Success { get; set; }
        public string ExtractedData { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public TimeSpan ProcessingTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
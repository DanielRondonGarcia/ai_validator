namespace DataValidator.Domain.Models
{
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

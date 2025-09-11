namespace AI.Library.Models
{
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
}

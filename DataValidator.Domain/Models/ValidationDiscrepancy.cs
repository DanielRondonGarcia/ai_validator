namespace DataValidator.Domain.Models
{
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

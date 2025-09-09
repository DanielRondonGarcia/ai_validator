using DataValidator.Domain.Models;

namespace DataValidator.Domain.Ports
{
    public interface IAiAnalysisProvider
    {
        Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt);
        string ProviderName { get; }
    }
}

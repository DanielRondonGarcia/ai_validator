using AI.Library.Models;

namespace AI.Library.Ports
{
    public interface IAiAnalysisProvider
    {
        Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt);
        string ProviderName { get; }
    }
}

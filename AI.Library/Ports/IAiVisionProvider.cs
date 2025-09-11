using AI.Library.Models;

namespace AI.Library.Ports
{
    public interface IAiVisionProvider
    {
        Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt);
        string ProviderName { get; }
    }
}

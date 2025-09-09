using DataValidator.Domain.Models;

namespace DataValidator.Domain.Ports
{
    public interface IAiVisionProvider
    {
        Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt);
        string ProviderName { get; }
    }
}

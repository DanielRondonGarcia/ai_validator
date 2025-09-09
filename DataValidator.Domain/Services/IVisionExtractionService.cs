using DataValidator.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataValidator.Domain.Services
{
    public interface IVisionExtractionService
    {
        Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string fileName, string documentType, List<string>? fieldsToExtract = null);
        Task<VisionExtractionResult> ExtractDataFromPdfAsync(byte[] pdfData, string fileName, string documentType, List<string>? fieldsToExtract = null);
    }
}

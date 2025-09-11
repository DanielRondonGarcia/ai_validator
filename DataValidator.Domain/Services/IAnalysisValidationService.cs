using DataValidator.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DataValidator.Domain.Services
{
    public interface IAnalysisValidationService
    {
        Task<ValidationAnalysisResult> ValidateExtractedDataAsync(string extractedData, string documentType, List<string> fieldsToValidate, string jsonData);
        Task<ValidationAnalysisResult> AnalyzeDiscrepanciesAsync(string extractedData, string documentType, List<string> discrepancies);
    }
}

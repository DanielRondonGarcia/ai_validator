using System.Collections.Generic;

namespace DataValidator.Domain.Services
{
    public interface IPromptBuilderService
    {
        string BuildExtractionPrompt(string documentType, List<string>? fieldsToExtract = null);
        string BuildValidationPrompt(string documentType, List<string> fieldsToValidate);
        string BuildDiscrepancyAnalysisPrompt(string documentType, List<string> discrepancies);
    }
}

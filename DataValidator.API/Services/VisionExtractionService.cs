using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using DataValidator.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataValidator.API.Services
{
    public class VisionExtractionService : IVisionExtractionService
    {
        private readonly ILogger<VisionExtractionService> _logger;
        private readonly IPromptBuilderService _promptBuilder;
        private readonly IPdfProcessor _pdfProcessor;
        private readonly IEnumerable<IAiVisionProvider> _visionProviders;
        private readonly AIModelsConfiguration _aiConfig;

        public VisionExtractionService(
            ILogger<VisionExtractionService> logger,
            IPromptBuilderService promptBuilder,
            IPdfProcessor pdfProcessor,
            IEnumerable<IAiVisionProvider> visionProviders,
            IOptions<AIModelsConfiguration> aiConfig)
        {
            _logger = logger;
            _promptBuilder = promptBuilder;
            _pdfProcessor = pdfProcessor;
            _visionProviders = visionProviders;
            _aiConfig = aiConfig.Value;
        }

        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string fileName, string documentType, List<string>? fieldsToExtract = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Starting vision extraction for image: {FileName}", fileName);

            var extractionPrompt = _promptBuilder.BuildExtractionPrompt(documentType, fieldsToExtract);
            var mimeType = GetMimeTypeFromFileName(fileName);

            var primaryProvider = _visionProviders.FirstOrDefault(p => p.ProviderName == _aiConfig.VisionModel.Provider);
            if (primaryProvider == null)
            {
                return new VisionExtractionResult { Success = false, ErrorMessage = $"Primary vision provider '{_aiConfig.VisionModel.Provider}' not found." };
            }

            var result = await primaryProvider.ExtractDataFromImageAsync(imageData, mimeType, extractionPrompt);

            stopwatch.Stop();
            result.ProcessingTime = stopwatch.Elapsed;
            _logger.LogInformation("Vision extraction completed in {ProcessingTime}ms. Success: {Success}", result.ProcessingTime.TotalMilliseconds, result.Success);
            return result;
        }

        public async Task<VisionExtractionResult> ExtractDataFromPdfAsync(byte[] pdfData, string fileName, string documentType, List<string>? fieldsToExtract = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Starting extraction from PDF: {FileName}", fileName);

            if (await _pdfProcessor.IsPdfImageBasedAsync(pdfData))
            {
                _logger.LogInformation("PDF is image-based. Converting pages to images.");
                var images = await _pdfProcessor.ConvertPdfPagesToImagesAsync(pdfData);
                var allExtractedData = new List<string>();
                VisionExtractionResult? lastResult = null;

                foreach (var (imageData, pageNumber) in images)
                {
                    var pageResult = await ExtractDataFromImageAsync(imageData, $"{fileName}_page_{pageNumber}.png", documentType, fieldsToExtract);
                    if (pageResult.Success)
                    {
                        allExtractedData.Add($"// Page {pageNumber}\n{pageResult.ExtractedData}");
                    }
                    lastResult = pageResult;
                }

                stopwatch.Stop();
                return new VisionExtractionResult
                {
                    Success = allExtractedData.Any(),
                    ExtractedData = string.Join("\n\n", allExtractedData),
                    ModelUsed = lastResult?.ModelUsed ?? "N/A",
                    Provider = lastResult?.Provider ?? "N/A",
                    ProcessingTime = stopwatch.Elapsed,
                };
            }
            else
            {
                _logger.LogInformation("PDF is text-based. Extracting text directly.");
                var extractedText = await _pdfProcessor.ExtractTextAsync(pdfData);
                stopwatch.Stop();
                return new VisionExtractionResult
                {
                    Success = !string.IsNullOrWhiteSpace(extractedText),
                    ExtractedData = extractedText,
                    ModelUsed = "Native PDF Text Extraction",
                    Provider = "PdfPig",
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        private string GetMimeTypeFromFileName(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream",
            };
        }
    }
}
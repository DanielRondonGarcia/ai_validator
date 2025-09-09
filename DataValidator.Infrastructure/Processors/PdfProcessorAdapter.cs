using DataValidator.Domain.Ports;
using IronPdf;
using IronSoftware.Drawing;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace DataValidator.Infrastructure.Processors
{
    public class PdfProcessorAdapter : IPdfProcessor
    {
        private readonly ILogger<PdfProcessorAdapter> _logger;

        public PdfProcessorAdapter(ILogger<PdfProcessorAdapter> logger)
        {
            _logger = logger;
            IronPdf.Installation.LinuxAndDockerDependenciesAutoConfig = true;
        }

        public async Task<List<(byte[] ImageData, int PageNumber)>> ConvertPdfPagesToImagesAsync(byte[] pdfData)
        {
            try
            {
                _logger.LogInformation("Converting PDF to images using IronPdf");
                var images = new List<(byte[], int)>();
                var pdfDocument = new IronPdf.PdfDocument(pdfData);

                for (int pageIndex = 0; pageIndex < pdfDocument.PageCount; pageIndex++)
                {
                    try
                    {
                        var pageImage = pdfDocument.ToBitmap()[pageIndex];
                        var imageBytes = pageImage.ExportBytes(AnyBitmap.ImageFormat.Png);
                        images.Add((imageBytes, pageIndex + 1));
                        _logger.LogInformation("Converted page {PageNumber} to image ({Size} bytes)", pageIndex + 1, imageBytes.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to convert page {PageNumber} to image", pageIndex + 1);
                    }
                }
                _logger.LogInformation("Successfully converted {PageCount} pages to images", images.Count);
                return await Task.FromResult(images);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PDF to images");
                return new List<(byte[], int)>();
            }
        }

        public async Task<string> ExtractTextAsync(byte[] pdfData)
        {
            try
            {
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfData);
                var extractedText = string.Join("\n", pdf.GetPages().Select(page => page.Text));
                return await Task.FromResult(extractedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                return string.Empty;
            }
        }

        public async Task<bool> IsPdfImageBasedAsync(byte[] pdfData)
        {
            try
            {
                var textContent = await ExtractTextAsync(pdfData);
                var wordCount = textContent.Split(new[] { ' ', '\n', '\r', '\t' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
                _logger.LogInformation("PDF text analysis - Word count: {WordCount}", wordCount);
                return wordCount < 50;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing PDF content, assuming image-based");
                return true;
            }
        }
    }
}

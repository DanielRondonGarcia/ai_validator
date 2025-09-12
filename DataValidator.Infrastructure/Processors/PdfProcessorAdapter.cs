using DataValidator.Domain.Ports;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using SkiaSharp;
using UglyToad.PdfPig.Rendering.Skia;
using UglyToad.PdfPig.Graphics.Colors;
using System;
using System.IO;

namespace DataValidator.Infrastructure.Processors
{
    public class PdfProcessorAdapter : IPdfProcessor
    {
        private readonly ILogger<PdfProcessorAdapter> _logger;

        public PdfProcessorAdapter(ILogger<PdfProcessorAdapter> logger)
        {
            _logger = logger;
        }

        public async Task<List<(byte[] ImageData, int PageNumber)>> ConvertPdfPagesToImagesAsync(byte[] pdfData)
        {
            try
            {
                _logger.LogInformation("Converting PDF to images using UglyToad.PdfPig.Rendering.Skia");
                var images = new List<(byte[], int)>();
                
                using (var document = PdfDocument.Open(pdfData, SkiaRenderingParsingOptions.Instance))
                {
                    document.AddSkiaPageFactory();
                
                    for (int pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
                    {
                        try
                        {
                            var pageNumber = pageIndex + 1;
                            using var ms = document.GetPageAsPng(pageNumber);
                            var imageBytes = ms.ToArray();
                            
                            // Validate generated PNG image
                            if (imageBytes == null || imageBytes.Length == 0)
                            {
                                _logger.LogError("Failed to generate PNG image for page {PageNumber}: empty or null data", pageNumber);
                                continue;
                            }
                            
                            // Verify PNG header (first 8 bytes should be PNG signature)
                            if (imageBytes.Length < 8 || 
                                imageBytes[0] != 0x89 || imageBytes[1] != 0x50 || imageBytes[2] != 0x4E || imageBytes[3] != 0x47 ||
                                imageBytes[4] != 0x0D || imageBytes[5] != 0x0A || imageBytes[6] != 0x1A || imageBytes[7] != 0x0A)
                            {
                                _logger.LogError("Generated image for page {PageNumber} does not have valid PNG header", pageNumber);
                                continue;
                            }
                            
                            // TEMPORAL: Guardar imagen en disco para validación
                            try
                            {
                                var tempDir = Path.Combine(Path.GetTempPath(), "pdf_validation_images");
                                Directory.CreateDirectory(tempDir);
                                var tempImagePath = Path.Combine(tempDir, $"page_{pageNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                                await File.WriteAllBytesAsync(tempImagePath, imageBytes);
                                _logger.LogInformation("TEMPORAL: Imagen guardada para validación en: {ImagePath}", tempImagePath);
                            }
                            catch (Exception tempEx)
                            {
                                _logger.LogWarning(tempEx, "No se pudo guardar la imagen temporal para validación");
                            }
                            
                            images.Add((imageBytes, pageNumber));
                            _logger.LogInformation("Successfully converted page {PageNumber} to valid PNG image ({Size} bytes)", pageNumber, imageBytes.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to convert page {PageNumber}", pageIndex + 1);
                        }
                    }
                }
                
                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert PDF to images");
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

using DataValidatorApi.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using IronPdf;
using IronSoftware.Drawing;

namespace DataValidatorApi.Services
{
    public class VisionExtractionService : IVisionExtractionService
    {
        private readonly AIModelsConfiguration _aiConfig;
        private readonly HttpClient _httpClient;
        private readonly ILogger<VisionExtractionService> _logger;
        private readonly IPromptBuilderService _promptBuilder;

        public VisionExtractionService(
            IOptions<AIModelsConfiguration> aiConfig,
            HttpClient httpClient,
            ILogger<VisionExtractionService> logger,
            IPromptBuilderService promptBuilder)
        {
            _aiConfig = aiConfig.Value;
            _httpClient = httpClient;
            _logger = logger;
            _promptBuilder = promptBuilder;
        }

        public async Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string fileName, string documentType, List<string>? fieldsToExtract = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting vision extraction for image: {FileName}, DocumentType: {DocumentType}, FileSize: {FileSize} bytes, FieldsToExtract: {FieldsCount}", 
                    fileName, documentType, imageData.Length, fieldsToExtract?.Count ?? 0);
                
                var base64Image = Convert.ToBase64String(imageData);
                var mimeType = GetMimeTypeFromFileName(fileName);
                
                _logger.LogDebug("Image converted to base64, MimeType: {MimeType}, Base64Length: {Base64Length}", mimeType, base64Image.Length);
                
                // Build specialized extraction prompt
                var extractionPrompt = _promptBuilder.BuildExtractionPrompt(documentType, fieldsToExtract);
                _logger.LogDebug("Extraction prompt built, Length: {PromptLength}", extractionPrompt.Length);
                
                // Try primary vision model first
                _logger.LogInformation("Attempting extraction with primary vision model: {Provider} - {Model}", 
                    _aiConfig.VisionModel.Provider, _aiConfig.VisionModel.Model);
                var result = await TryExtractWithModel(_aiConfig.VisionModel, base64Image, mimeType, extractionPrompt);
                
                if (!result.Success && _aiConfig.AlternativeVisionModel.Provider != _aiConfig.VisionModel.Provider)
                {
                    _logger.LogWarning("Primary vision model failed with error: {Error}. Trying alternative model: {Provider} - {Model}", 
                        result.ErrorMessage, _aiConfig.AlternativeVisionModel.Provider, _aiConfig.AlternativeVisionModel.Model);
                    result = await TryExtractWithModel(_aiConfig.AlternativeVisionModel, base64Image, mimeType, extractionPrompt);
                }
                
                stopwatch.Stop();
                result.ProcessingTime = stopwatch.Elapsed;
                result.Metadata["FileSize"] = imageData.Length;
                result.Metadata["FileName"] = fileName;
                result.Metadata["DocumentType"] = documentType;
                result.Metadata["FieldsToExtract"] = fieldsToExtract?.Count ?? 0;
                
                _logger.LogInformation("Vision extraction completed. Success: {Success}, ProcessingTime: {ProcessingTime}ms, ExtractedDataLength: {ExtractedDataLength}", 
                    result.Success, result.ProcessingTime.TotalMilliseconds, result.ExtractedData?.Length ?? 0);
                
                if (!result.Success)
                {
                    _logger.LogError("Vision extraction failed: {ErrorMessage}", result.ErrorMessage);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting data from image: {FileName}", fileName);
                stopwatch.Stop();
                
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        public async Task<VisionExtractionResult> ExtractDataFromPdfAsync(byte[] pdfData, string fileName, string documentType, List<string>? fieldsToExtract = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogInformation("Starting vision extraction for PDF: {FileName}, DocumentType: {DocumentType}", fileName, documentType);
                
                // Check if PDF is image-based
                var isImageBased = await IsPdfImageBasedAsync(pdfData);
                
                if (isImageBased)
                {
                    // Convert PDF pages to images and process with vision model
                    var images = await ConvertPdfPagesToImagesAsync(pdfData);
                    var allExtractedData = new List<string>();
                    
                    foreach (var (imageData, pageNumber) in images)
                    {
                        var pageResult = await ExtractDataFromImageAsync(imageData, $"{fileName}_page_{pageNumber}", documentType, fieldsToExtract);
                        if (pageResult.Success)
                        {
                            allExtractedData.Add($"Page {pageNumber}: {pageResult.ExtractedData}");
                        }
                    }
                    
                    stopwatch.Stop();
                    
                    return new VisionExtractionResult
                    {
                        Success = allExtractedData.Any(),
                        ExtractedData = string.Join("\n\n", allExtractedData),
                        ModelUsed = _aiConfig.VisionModel.Model,
                        Provider = _aiConfig.VisionModel.Provider,
                        ProcessingTime = stopwatch.Elapsed,
                        Metadata = new Dictionary<string, object>
                        {
                            ["FileSize"] = pdfData.Length,
                            ["FileName"] = fileName,
                            ["DocumentType"] = documentType,
                            ["FieldsToExtract"] = fieldsToExtract?.Count ?? 0,
                            ["PagesProcessed"] = images.Count,
                            ["IsImageBased"] = true
                        }
                    };
                }
                else
                {
                    // Extract text from PDF and process with analysis model
                    using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfData);
                    var extractedText = string.Join("\n", pdf.GetPages().Select(page => page.Text));
                    
                    stopwatch.Stop();
                    
                    return new VisionExtractionResult
                    {
                        Success = !string.IsNullOrWhiteSpace(extractedText),
                        ExtractedData = extractedText,
                        ModelUsed = "PDF Text Extraction",
                        Provider = "Native",
                        ProcessingTime = stopwatch.Elapsed,
                        Metadata = new Dictionary<string, object>
                        {
                            ["FileSize"] = pdfData.Length,
                            ["FileName"] = fileName,
                            ["DocumentType"] = documentType,
                            ["FieldsToExtract"] = fieldsToExtract?.Count ?? 0,
                            ["IsImageBased"] = false,
                            ["TextLength"] = extractedText.Length
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting data from PDF: {FileName}", fileName);
                stopwatch.Stop();
                
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed
                };
            }
        }

        public async Task<bool> IsPdfImageBasedAsync(byte[] pdfData)
        {
            try
            {
                using var pdf = UglyToad.PdfPig.PdfDocument.Open(pdfData);
                var textContent = string.Join("\n", pdf.GetPages().Select(page => page.Text));
                
                // If there's very little text content, it's likely image-based
                var wordCount = textContent.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                
                _logger.LogInformation("PDF text analysis - Word count: {WordCount}", wordCount);
                
                // Consider it image-based if less than 50 words total
                return wordCount < 50;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error analyzing PDF content, assuming image-based");
                return true; // Assume image-based if we can't extract text
            }
        }

        private async Task<VisionExtractionResult> TryExtractWithModel(AIModelConfig modelConfig, string base64Image, string mimeType, string extractionPrompt)
        {
            try
            {
                if (modelConfig.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExtractWithOpenAI(modelConfig, base64Image, mimeType, extractionPrompt);
                }
                else if (modelConfig.Provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
                {
                    return await ExtractWithGemini(modelConfig, base64Image, mimeType, extractionPrompt);
                }
                else
                {
                    return new VisionExtractionResult
                    {
                        Success = false,
                        ErrorMessage = $"Unsupported provider: {modelConfig.Provider}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error with model {Model} from provider {Provider}", modelConfig.Model, modelConfig.Provider);
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ModelUsed = modelConfig.Model,
                    Provider = modelConfig.Provider
                };
            }
        }

        private async Task<VisionExtractionResult> ExtractWithOpenAI(AIModelConfig config, string base64Image, string mimeType, string extractionPrompt)
        {
            var requestBody = new
            {
                model = config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = extractionPrompt },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:{mimeType};base64,{base64Image}"
                                }
                            }
                        }
                    }
                },
                max_tokens = config.MaxTokens,
                temperature = config.Temperature
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

            var response = await _httpClient.PostAsync($"{config.BaseUrl}/chat/completions", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var extractedText = jsonResponse.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                return new VisionExtractionResult
                {
                    Success = true,
                    ExtractedData = extractedText,
                    ModelUsed = config.Model,
                    Provider = config.Provider
                };
            }
            else
            {
                _logger.LogError("OpenAI API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"OpenAI API error: {response.StatusCode}",
                    ModelUsed = config.Model,
                    Provider = config.Provider
                };
            }
        }

        private async Task<VisionExtractionResult> ExtractWithGemini(AIModelConfig config, string base64Image, string mimeType, string extractionPrompt)
        {
            // Simplified Gemini implementation - would need proper Gemini API integration
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = extractionPrompt },
                            new
                            {
                                inline_data = new
                                {
                                    mime_type = mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    maxOutputTokens = config.MaxTokens,
                    temperature = config.Temperature
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{config.BaseUrl}/models/{config.Model}:generateContent?key={config.ApiKey}";
            var response = await _httpClient.PostAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                var extractedText = jsonResponse.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

                return new VisionExtractionResult
                {
                    Success = true,
                    ExtractedData = extractedText,
                    ModelUsed = config.Model,
                    Provider = config.Provider
                };
            }
            else
            {
                _logger.LogError("Gemini API error: {StatusCode} - {Response}", response.StatusCode, responseContent);
                return new VisionExtractionResult
                {
                    Success = false,
                    ErrorMessage = $"Gemini API error: {response.StatusCode}",
                    ModelUsed = config.Model,
                    Provider = config.Provider
                };
            }
        }

        private async Task<List<(byte[] ImageData, int PageNumber)>> ConvertPdfPagesToImagesAsync(byte[] pdfData)
        {
            try
            {
                _logger.LogInformation("Converting PDF to images using IronPdf");
                
                var images = new List<(byte[], int)>();
                
                // Load PDF from byte array
                var pdfDocument = new IronPdf.PdfDocument(pdfData);
                
                // Convert each page to image
                for (int pageIndex = 0; pageIndex < pdfDocument.PageCount; pageIndex++)
                {
                    try
                    {
                        // Convert all pages to images and get the specific page
                        var allPageImages = pdfDocument.ToBitmap();
                        if (pageIndex >= allPageImages.Length) continue;
                        var pageImage = allPageImages[pageIndex];
                        
                        // Convert bitmap to byte array
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
                return images;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting PDF to images");
                return new List<(byte[], int)>();
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
                _ => "image/jpeg" // Default fallback
            };
        }
    }
}
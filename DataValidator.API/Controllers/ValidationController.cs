using Microsoft.AspNetCore.Mvc;
using IronPdf;
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using IronSoftware.Drawing;
using GenerativeAI;
using GenerativeAI.Types;
using OpenAI;
using OpenAI.Chat;
using DataValidatorApi.Models;
using DataValidatorApi.Services;
using System.Text.Json;

namespace DataValidatorApi.Controllers
{
    /// <summary>
    /// Controlador inteligente para la validación de documentos que detecta automáticamente el proveedor de IA disponible.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ValidationController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IVisionExtractionService _visionExtractionService;
        private readonly IAnalysisValidationService _analysisValidationService;
        private readonly ILogger<ValidationController> _logger;

        /// <summary>
        /// Constructor for ValidationController.
        /// </summary>
        public ValidationController(
            IConfiguration configuration,
            IVisionExtractionService visionExtractionService,
            IAnalysisValidationService analysisValidationService,
            ILogger<ValidationController> logger)
        {
            _configuration = configuration;
            _visionExtractionService = visionExtractionService;
            _analysisValidationService = analysisValidationService;
            _logger = logger;
            IronPdf.Installation.LinuxAndDockerDependenciesAutoConfig = true;
        }

        /// <summary>
        /// Valida cruzadamente un documento PDF comparando datos extraídos por IA con datos proporcionados.
        /// Detecta automáticamente el proveedor de IA disponible.
        /// </summary>
        /// <param name="request">Solicitud de validación cruzada que incluye el archivo, datos JSON y configuración.</param>
        /// <returns>Resultado detallado de la validación cruzada.</returns>
        /// <response code="200">Devuelve el resultado de la validación cruzada.</response>
        /// <response code="400">Si la solicitud es inválida o faltan datos requeridos.</response>
        /// <response code="500">Si ocurre un error interno durante el procesamiento.</response>
        [HttpPost("cross-validate")]
        public async Task<IActionResult> CrossValidate([FromForm] CrossValidationRequest request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] Iniciando validación cruzada con nueva arquitectura de dos fases. FileName: {FileName}, FileSize: {FileSize} bytes, DocumentType: {DocumentType}, FieldsToValidate: {FieldsCount}", 
                requestId, request.File?.FileName ?? "unknown", request.File?.Length ?? 0, request.DocumentType ?? "document", request.FieldsToValidate?.Count ?? 0);
            
            // Validaciones básicas
            if (request.File == null || request.File.Length == 0)
            {
                _logger.LogError("[{RequestId}] Archivo no proporcionado o vacío", requestId);
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(request.JsonData))
            {
                _logger.LogError("[{RequestId}] Datos JSON no proporcionados", requestId);
                return BadRequest("JSON data is not provided.");
            }

            _logger.LogDebug("[{RequestId}] JSON data received: {JsonData}", requestId, request.JsonData);

            // Validar tipos de archivo soportados
            var supportedTypes = new[] { "application/pdf", "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
            if (!supportedTypes.Contains(request.File.ContentType.ToLower()))
            {
                _logger.LogError("[{RequestId}] Tipo de archivo inválido: {ContentType}", requestId, request.File.ContentType);
                return BadRequest($"Invalid file type: {request.File.ContentType}. Supported types: PDF, JPEG, PNG, GIF, BMP, WebP.");
            }
            
            var isImage = request.File.ContentType.StartsWith("image/");
            var isPdf = request.File.ContentType == "application/pdf";
            _logger.LogInformation("[{RequestId}] Tipo de archivo detectado: {FileType}", requestId, isImage ? "Imagen" : "PDF");

            _logger.LogInformation("[{RequestId}] Archivo recibido: {FileName}, Tamaño: {FileSize} bytes", requestId, request.File.FileName, request.File.Length);
            _logger.LogInformation("[{RequestId}] Datos JSON recibidos: {JsonPreview}...", requestId, request.JsonData.Substring(0, Math.Min(100, request.JsonData.Length)));

            try
            {
                // FASE 1: Extracción de datos con modelo especializado en visión
                _logger.LogInformation("[{RequestId}] FASE 1: Iniciando extracción de datos con modelo de visión especializado", requestId);
                
                VisionExtractionResult extractionResult;
                // Convert IFormFile to byte array
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await request.File.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                if (isPdf)
                {
                    extractionResult = await _visionExtractionService.ExtractDataFromPdfAsync(fileData, request.File.FileName ?? "document.pdf", request.DocumentType ?? "document", request.FieldsToValidate);
                }
                else
                {
                    extractionResult = await _visionExtractionService.ExtractDataFromImageAsync(fileData, request.File.FileName ?? "image", request.DocumentType ?? "document", request.FieldsToValidate);
                }

                if (!extractionResult.Success)
                {
                    _logger.LogError("[{RequestId}] Error en la extracción de datos: {Error}", requestId, extractionResult.ErrorMessage);
                    return StatusCode(500, $"Error during data extraction: {extractionResult.ErrorMessage}");
                }

                _logger.LogInformation("[{RequestId}] FASE 1 completada. Modelo usado: {Model}, ProcessingTime: {ProcessingTime}ms", requestId, extractionResult.ModelUsed, extractionResult.ProcessingTime.TotalMilliseconds);
                _logger.LogDebug("[{RequestId}] Datos extraídos: {ExtractedData}", requestId, extractionResult.ExtractedData);

                // FASE 2: Análisis y validación con modelo general
                _logger.LogInformation("[{RequestId}] FASE 2: Iniciando análisis y validación con modelo general", requestId);
                
                var validationResult = await _analysisValidationService.ValidateExtractedDataAsync(
                    extractionResult.ExtractedData,
                    request.DocumentType ?? "document",
                    request.FieldsToValidate);

                if (!validationResult.Success)
                {
                    _logger.LogError("[{RequestId}] Error en la validación: {Error}", requestId, validationResult.ErrorMessage);
                    return StatusCode(500, $"Error during validation: {validationResult.ErrorMessage}");
                }

                _logger.LogInformation("[{RequestId}] FASE 2 completada. Modelo usado: {Model}, ProcessingTime: {ProcessingTime}ms", requestId, validationResult.ModelUsed, validationResult.ProcessingTime.TotalMilliseconds);
                _logger.LogInformation("[{RequestId}] Validación completada. Es válido: {IsValid}, Issues: {IssuesCount}", requestId, validationResult.IsValid, validationResult.Discrepancies?.Count ?? 0);

                // Crear respuesta combinada
                var totalProcessingTime = extractionResult.ProcessingTime + validationResult.ProcessingTime;
                var response = new
                {
                    Success = true,
                    ExtractionPhase = new
                    {
                        ModelUsed = extractionResult.ModelUsed,
                        ExtractedData = extractionResult.ExtractedData,
                        Metadata = extractionResult.Metadata
                    },
                    ValidationPhase = new
                    {
                        ModelUsed = validationResult.ModelUsed,
                        IsValid = validationResult.IsValid,
                        Analysis = validationResult.Analysis,
                        Discrepancies = validationResult.Discrepancies,
                        Confidence = validationResult.ConfidenceScore
                    },
                    ProcessingTime = DateTime.UtcNow
                };

                _logger.LogInformation("[{RequestId}] Validación cruzada completada exitosamente. TotalProcessingTime: {TotalTime}ms, IsValid: {IsValid}, IssuesCount: {IssuesCount}", 
                    requestId, totalProcessingTime.TotalMilliseconds, validationResult.IsValid, validationResult.Discrepancies?.Count ?? 0);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error durante la validación cruzada: {Message}", requestId, ex.Message);
                return StatusCode(500, $"An error occurred during cross-validation: {ex.Message}");
            }
        }

        // Los métodos de detección de proveedores han sido movidos a los servicios especializados
        // para una mejor separación de responsabilidades y reutilización

        // Métodos obsoletos eliminados - la funcionalidad ahora se maneja en servicios especializados:
        // - ProcessWithOpenAI: Movido a VisionExtractionService
        // - ProcessWithGemini: Movido a VisionExtractionService
        // - ConvertPdfToImage: Movido a VisionExtractionService
        // - ConvertPdfFileToImage: Movido a VisionExtractionService
        // - IsPdfImageBased: Movido a VisionExtractionService



        // Métodos de validación obsoletos eliminados - ahora se usan los endpoints refactorizados:
        // - ValidatePdf: Funcionalidad movida al método CrossValidate refactorizado
        // - ValidateImage: Funcionalidad movida al método CrossValidate refactorizado
        // La nueva arquitectura usa servicios especializados para mejor separación de responsabilidades

        // Métodos de extracción y comparación obsoletos eliminados - funcionalidad movida a servicios especializados:
        // - ExtractInformationFromImage: Movido a VisionExtractionService
        // - CreateExtractionPrompt: Movido a VisionExtractionService
        // - ExtractWithGemini: Movido a VisionExtractionService
        // - ExtractWithOpenAI: Movido a VisionExtractionService
        // - CompareDataWithAI: Movido a AnalysisValidationService
        // - CreateComparisonPrompt: Movido a AnalysisValidationService
        // - GetComparisonFromOpenAI: Movido a AnalysisValidationService
        // - GetComparisonFromGemini: Movido a AnalysisValidationService
        // - ParseValidationResponse: Movido a AnalysisValidationService
    }
}

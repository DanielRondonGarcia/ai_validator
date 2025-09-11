using DataValidator.API.Models;
using DataValidator.Domain.Models;
using DataValidator.Domain.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DataValidator.API.Controllers
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
        }

        /// <summary>
        /// Valida cruzadamente un documento PDF comparando datos extraídos por IA con datos proporcionados.
        /// Detecta automáticamente el proveedor de IA disponible.
        /// </summary>
        /// <param name="request">Solicitud de validación cruzada que incluye el archivo, datos JSON y configuración.</param>
        /// <returns>Resultado detallado de la validación cruzada.</returns>
        [HttpPost("cross-validate")]
        public async Task<IActionResult> CrossValidate([FromForm] CrossValidationRequest request)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("[{RequestId}] Iniciando validación cruzada. FileName: {FileName}, DocumentType: {DocumentType}",
                requestId, request.File?.FileName ?? "unknown", request.DocumentType ?? "document");

            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(request.JsonData))
            {
                return BadRequest("JSON data is not provided.");
            }

            var supportedTypes = new[] { "application/pdf", "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
            if (!supportedTypes.Contains(request.File.ContentType.ToLower()))
            {
                return BadRequest($"Invalid file type: {request.File.ContentType}.");
            }

            var isPdf = request.File.ContentType == "application/pdf";

            try
            {
                byte[] fileData;
                using (var memoryStream = new MemoryStream())
                {
                    await request.File.CopyToAsync(memoryStream);
                    fileData = memoryStream.ToArray();
                }

                VisionExtractionResult extractionResult;
                if (isPdf)
                {
                    extractionResult = await _visionExtractionService.ExtractDataFromPdfAsync(fileData, request.File.FileName, request.DocumentType, request.FieldsToValidate);
                }
                else
                {
                    extractionResult = await _visionExtractionService.ExtractDataFromImageAsync(fileData, request.File.FileName, request.DocumentType, request.FieldsToValidate);
                }

                if (!extractionResult.Success)
                {
                    return StatusCode(500, $"Error during data extraction: {extractionResult.ErrorMessage}");
                }

                var validationResult = await _analysisValidationService.ValidateExtractedDataAsync(
                    extractionResult.ExtractedData,
                    request.DocumentType ?? "document",
                    request.FieldsToValidate ?? new(),
                    request.JsonData);

                if (!validationResult.Success)
                {
                    return StatusCode(500, $"Error during validation: {validationResult.ErrorMessage}");
                }

                var response = new
                {
                    Success = true,
                    ExtractionPhase = extractionResult,
                    ValidationPhase = validationResult
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{RequestId}] Error durante la validación cruzada: {Message}", requestId, ex.Message);
                return StatusCode(500, $"An error occurred during cross-validation: {ex.Message}");
            }
        }
    }
}

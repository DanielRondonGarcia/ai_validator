using Microsoft.AspNetCore.Mvc;
using IronPdf;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using IronSoftware.Drawing;
using GenerativeAI;
using GenerativeAI.Types;

namespace DataValidatorApi.Controllers
{
    /// <summary>
    /// Controlador para la validación de documentos usando Google Gemini AI.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ValidationController : ControllerBase
    {
        /// <summary>
        /// Constructor for ValidationController.
        /// </summary>
        public ValidationController()
        {
            IronPdf.Installation.LinuxAndDockerDependenciesAutoConfig = true;
        }

        /// <summary>
        /// Analiza el contenido de un archivo PDF usando el modelo Gemini Pro Vision de Google.
        /// </summary>
        /// <remarks>
        /// Sube un archivo PDF y una pregunta o prompt en lenguaje natural.
        /// La API convertirá la primera página del PDF en una imagen y la enviará a Gemini
        /// junto con tu pregunta en una única petición multimodal.
        /// **Importante:** Se requiere una clave de API de Google. La aplicación la buscará
        /// en la variable de entorno `GOOGLE_API_KEY`.
        ///
        /// **Nota sobre la implementación:** La imagen se guarda en un archivo temporal en disco antes de ser enviada.
        /// Esto se debe a dificultades para determinar la API correcta para el envío de datos en memoria con el SDK actual.
        /// Es una solución funcional pero podría mejorarse en el futuro.
        ///
        /// Ejemplo de prompt:
        ///
        ///     "¿Este documento es un certificado de finalización para el curso de .NET? ¿A nombre de quién está?"
        ///
        /// </remarks>
        /// <param name="file">El archivo PDF a validar.</param>
        /// <param name="schema">La pregunta o prompt en lenguaje natural para la IA de Google.</param>
        /// <returns>La respuesta de texto generada por el modelo de IA.</returns>
        /// <response code="200">Devuelve la respuesta de la IA.</response>
        /// <response code="400">Si el archivo, el prompt o la clave de API no se proporcionan.</response>
        /// <response code="500">Si ocurre un error interno durante el procesamiento.</response>
        [HttpPost("validate")]
        public async Task<IActionResult> Validate(IFormFile file, [FromForm] string schema)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(schema))
            {
                return BadRequest("Prompt (schema) is not provided.");
            }

            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest("Google API Key is not configured. Please set the GOOGLE_API_KEY environment variable.");
            }

            if (file.ContentType != "application/pdf")
            {
                return BadRequest("Invalid file type. Only PDF is supported for now.");
            }

            string? tempFilePath = null;
            try
            {
                // 1. Convert PDF page to an in-memory image using IronPDF
                await using var pdfStream = new MemoryStream();
                await file.CopyToAsync(pdfStream);
                pdfStream.Position = 0;
                var pdf = new PdfDocument(pdfStream);

                if (pdf.PageCount == 0)
                {
                    return BadRequest("The provided PDF has no pages.");
                }

                AnyBitmap? pageBitmap = pdf.ToBitmap().FirstOrDefault();
                if (pageBitmap == null)
                {
                    return StatusCode(500, "Failed to convert PDF page to image.");
                }

                // Save image to a temporary file
                tempFilePath = Path.GetTempFileName() + ".png";
                pageBitmap.SaveAs(tempFilePath);

                // 2. Call Google Gemini AI
                var googleAI = new GoogleAi(apiKey);
                var generativeModel = googleAI.CreateGenerativeModel("gemini-1.5-flash-latest");

                var request = new GenerateContentRequest();
                request.AddText(schema);
                request.AddInlineFile(tempFilePath);

                var response = await generativeModel.GenerateContentAsync(request);

                return Ok(new {
                    FileName = file.FileName,
                    Prompt = schema,
                    AI_Response = response.Text()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while processing the file: {ex.Message}");
            }
            finally
            {
                if (tempFilePath != null && System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
            }
        }
    }
}

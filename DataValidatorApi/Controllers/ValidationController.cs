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

        /// <summary>
        /// Constructor for ValidationController.
        /// </summary>
        public ValidationController(IConfiguration configuration)
        {
            _configuration = configuration;
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
            Console.WriteLine("[INFO] Iniciando validación cruzada");
            
            // Validaciones básicas
            if (request.File == null || request.File.Length == 0)
            {
                Console.WriteLine("[ERROR] Archivo no proporcionado o vacío");
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(request.JsonData))
            {
                Console.WriteLine("[ERROR] Datos JSON no proporcionados");
                return BadRequest("JSON data is not provided.");
            }

            // Validar tipos de archivo soportados
            var supportedTypes = new[] { "application/pdf", "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
            if (!supportedTypes.Contains(request.File.ContentType.ToLower()))
            {
                Console.WriteLine($"[ERROR] Tipo de archivo inválido: {request.File.ContentType}");
                return BadRequest($"Invalid file type: {request.File.ContentType}. Supported types: PDF, JPEG, PNG, GIF, BMP, WebP.");
            }
            
            var isImage = request.File.ContentType.StartsWith("image/");
            var isPdf = request.File.ContentType == "application/pdf";
            Console.WriteLine($"[INFO] Tipo de archivo detectado: {(isImage ? "Imagen" : "PDF")}");

            Console.WriteLine($"[INFO] Archivo recibido: {request.File.FileName}, Tamaño: {request.File.Length} bytes");
            Console.WriteLine($"[INFO] Datos JSON recibidos: {request.JsonData.Substring(0, Math.Min(100, request.JsonData.Length))}...");

            try
            {
                Console.WriteLine("[INFO] Detectando proveedor de IA disponible...");
                // Detectar automáticamente el proveedor disponible
                var availableProvider = await DetectAvailableProvider();
                if (string.IsNullOrEmpty(availableProvider))
                {
                    Console.WriteLine("[ERROR] No hay proveedores de IA disponibles");
                    return StatusCode(500, "No AI provider is available. Please configure OPENAI_API_KEY or GOOGLE_API_KEY environment variables.");
                }

                Console.WriteLine($"[INFO] Proveedor detectado: {availableProvider}");

                // Procesar directamente con el proveedor detectado
                request.Provider = availableProvider;
                
                // Guardar archivo temporalmente
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                string processedFilePath = tempFilePath;
                
                try
                {
                    
                    // Lógica inteligente de procesamiento según tipo de archivo
                    if (isImage)
                    {
                        Console.WriteLine("[INFO] Procesando imagen directamente...");
                        // Las imágenes se procesan directamente
                    }
                    else if (isPdf)
                    {
                        Console.WriteLine("[INFO] Procesando PDF...");
                        
                        // Si es OpenAI, siempre convertir PDF a imagen
                        if (availableProvider.ToLower() == "openai")
                        {
                            Console.WriteLine("[INFO] Convirtiendo PDF a imagen para OpenAI...");
                            processedFilePath = await ConvertPdfToImage(request.File);
                        }
                        else
                        {
                            // Para Gemini, intentar procesar PDF directamente primero
                            Console.WriteLine("[INFO] Intentando procesar PDF directamente con Gemini...");
                            
                            // Verificar si el PDF contiene principalmente imágenes
                            var isPdfImageBased = await IsPdfImageBased(tempFilePath);
                            if (isPdfImageBased)
                            {
                                Console.WriteLine("[INFO] PDF detectado como basado en imágenes, convirtiendo a imagen...");
                                processedFilePath = await ConvertPdfFileToImage(tempFilePath);
                            }
                        }
                    }

                    Console.WriteLine($"[INFO] Iniciando extracción de información con {availableProvider}...");
                    // Extraer información del archivo procesado
                    var extractedInfo = await ExtractInformationFromImage(processedFilePath, availableProvider, request.DocumentType);
                    Console.WriteLine($"[INFO] Información extraída exitosamente");

                    Console.WriteLine($"[INFO] Iniciando comparación de datos...");
                    // Comparar con los datos JSON proporcionados
                    var result = await CompareDataWithAI(extractedInfo, request.JsonData, availableProvider, request.FieldsToValidate);
                    Console.WriteLine($"[INFO] Validación cruzada completada");

                    return Ok(result);
                }
                finally
                {
                    // Limpiar archivos temporales
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                    
                    // Limpiar archivo procesado si es diferente del original
                    if (processedFilePath != tempFilePath && System.IO.File.Exists(processedFilePath))
                    {
                        System.IO.File.Delete(processedFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error durante la validación cruzada: {ex.Message}");
                Console.WriteLine($"[ERROR] Stack trace: {ex.StackTrace}");
                return StatusCode(500, $"An error occurred during cross-validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Detecta qué proveedor de IA está disponible y funcional
        /// </summary>
        /// <returns>El nombre del proveedor disponible o null si ninguno está disponible</returns>
        private async Task<string?> DetectAvailableProvider()
        {
            Console.WriteLine("[INFO] Verificando disponibilidad de OpenAI...");
            // Verificar OpenAI primero (generalmente más rápido)
            if (await IsOpenAIAvailable())
            {
                Console.WriteLine("[INFO] OpenAI está disponible");
                return "openai";
            }

            Console.WriteLine("[INFO] OpenAI no disponible, verificando Gemini...");
            // Verificar Gemini como alternativa
            if (await IsGeminiAvailable())
            {
                Console.WriteLine("[INFO] Gemini está disponible");
                return "gemini";
            }

            Console.WriteLine("[WARNING] Ningún proveedor de IA está disponible");
            return null;
        }

        /// <summary>
        /// Verifica si OpenAI está disponible
        /// </summary>
        private async Task<bool> IsOpenAIAvailable()
        {
            try
            {
                Console.WriteLine("[DEBUG] Verificando clave de OpenAI...");
                // Leer primero desde appsettings, luego desde variables de entorno
                var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    Console.WriteLine("[DEBUG] Clave de OpenAI no encontrada");
                    return false;
                }

                Console.WriteLine($"[DEBUG] Clave de OpenAI encontrada (longitud: {apiKey.Length})");
                Console.WriteLine("[DEBUG] Creando cliente OpenAI...");
                
                // Hacer una prueba rápida con OpenAI
                var openAiClient = new OpenAIClient(apiKey);
                var chatClient = openAiClient.GetChatClient("gpt-4o");

                Console.WriteLine("[DEBUG] Preparando mensaje de prueba...");
                var testMessages = new List<ChatMessage>
                {
                    new UserChatMessage("Test")
                };

                var testOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 1
                };

                Console.WriteLine("[DEBUG] Enviando solicitud de prueba a OpenAI...");
                await chatClient.CompleteChatAsync(testMessages, testOptions);
                Console.WriteLine("[DEBUG] Prueba de OpenAI exitosa");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error en prueba de OpenAI: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si Gemini está disponible
        /// </summary>
        private async Task<bool> IsGeminiAvailable()
        {
            try
            {
                // Leer primero desde appsettings, luego desde variables de entorno
                var apiKey = _configuration["AI:Google:ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    return false;
                }

                // Hacer una prueba rápida con Gemini
                var model = new GenerativeModel(apiKey, "gemini-1.5-flash-latest");
                var content = new Content();
                content.AddText("Test");

                var request = new GenerateContentRequest();
                request.Contents.Add(content);

                await model.GenerateContentAsync(request);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Procesa una imagen con OpenAI Vision
        /// </summary>
        /// <param name="imagePath">Ruta de la imagen temporal</param>
        /// <param name="prompt">Prompt para la IA</param>
        /// <returns>Respuesta de OpenAI</returns>
        private async Task<string> ProcessWithOpenAI(string imagePath, string prompt)
        {
            Console.WriteLine("[INFO] Iniciando procesamiento con OpenAI...");
            var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var openAiClient = new OpenAIClient(apiKey!);
            var chatClient = openAiClient.GetChatClient("gpt-4o");

            Console.WriteLine($"[INFO] Leyendo imagen desde: {imagePath}");
            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
            Console.WriteLine($"[INFO] Imagen leída, tamaño: {imageBytes.Length} bytes");

            var messages = new List<ChatMessage>
            {
                new UserChatMessage(
                    ChatMessageContentPart.CreateTextPart(prompt),
                    ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), "image/png")
                )
            };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 1000
            };

            Console.WriteLine("[INFO] Enviando solicitud a OpenAI...");
            var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            Console.WriteLine("[INFO] Respuesta recibida de OpenAI");
            return response.Value.Content[0].Text;
        }

        /// <summary>
        /// Procesa una imagen con Google Gemini
        /// </summary>
        /// <param name="imagePath">Ruta de la imagen temporal</param>
        /// <param name="prompt">Prompt para la IA</param>
        /// <returns>Respuesta de Gemini</returns>
        private async Task<string> ProcessWithGemini(string imagePath, string prompt)
        {
            var apiKey = _configuration["AI:Google:ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            var googleAI = new GoogleAi(apiKey!);
            var generativeModel = googleAI.CreateGenerativeModel("gemini-1.5-flash-latest");

            var request = new GenerateContentRequest();
            request.AddText(prompt);
            request.AddInlineFile(imagePath);

            var response = await generativeModel.GenerateContentAsync(request);
            return response.Text();
        }

        /// <summary>
        /// Convierte la primera página de un PDF a una imagen PNG temporal.
        /// </summary>
        /// <param name="file">El archivo PDF a convertir.</param>
        /// <returns>La ruta del archivo temporal de imagen creado.</returns>
        private async Task<string> ConvertPdfToImage(IFormFile file)
        {
            await using var pdfStream = new MemoryStream();
            await file.CopyToAsync(pdfStream);
            pdfStream.Position = 0;
            var pdf = new PdfDocument(pdfStream);

            if (pdf.PageCount == 0)
            {
                throw new InvalidOperationException("The provided PDF has no pages.");
            }

            AnyBitmap? pageBitmap = pdf.ToBitmap().FirstOrDefault();
            if (pageBitmap == null)
            {
                throw new InvalidOperationException("Failed to convert PDF page to image.");
            }

            // Save image to a temporary file
            var tempFilePath = Path.GetTempFileName() + ".png";
            pageBitmap.SaveAs(tempFilePath);

            return tempFilePath;
        }

        /// <summary>
        /// Convierte la primera página de un PDF desde archivo a una imagen PNG temporal.
        /// </summary>
        /// <param name="pdfFilePath">La ruta del archivo PDF a convertir.</param>
        /// <returns>La ruta del archivo temporal de imagen creado.</returns>
        private async Task<string> ConvertPdfFileToImage(string pdfFilePath)
        {
            var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfFilePath);
            await using var pdfStream = new MemoryStream(pdfBytes);
            var pdf = new PdfDocument(pdfStream);

            if (pdf.PageCount == 0)
            {
                throw new InvalidOperationException("The provided PDF has no pages.");
            }

            AnyBitmap? pageBitmap = pdf.ToBitmap().FirstOrDefault();
            if (pageBitmap == null)
            {
                throw new InvalidOperationException("Failed to convert PDF page to image.");
            }

            // Save image to a temporary file
            var tempFilePath = Path.GetTempFileName() + ".png";
            pageBitmap.SaveAs(tempFilePath);

            return tempFilePath;
        }

        /// <summary>
        /// Determina si un PDF está basado principalmente en imágenes
        /// </summary>
        /// <param name="pdfFilePath">La ruta del archivo PDF a analizar.</param>
        /// <returns>True si el PDF contiene principalmente imágenes, False si contiene texto extraíble.</returns>
        private async Task<bool> IsPdfImageBased(string pdfFilePath)
        {
            try
            {
                var pdfBytes = await System.IO.File.ReadAllBytesAsync(pdfFilePath);
                await using var pdfStream = new MemoryStream(pdfBytes);
                var pdf = new PdfDocument(pdfStream);

                if (pdf.PageCount == 0)
                {
                    return false;
                }

                // Intentar extraer texto de todo el PDF
                var extractedText = pdf.ExtractAllText();
                
                // Si hay poco o ningún texto extraíble, probablemente es una imagen
                var textLength = extractedText?.Trim().Length ?? 0;
                Console.WriteLine($"[INFO] Texto extraído del PDF: {textLength} caracteres");
                
                // Si tiene menos de 50 caracteres de texto, considerarlo como imagen
                return textLength < 50;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARNING] Error al analizar PDF, asumiendo que es basado en imágenes: {ex.Message}");
                return true; // En caso de error, asumir que es imagen para mayor compatibilidad
            }
        }



        /// <summary>
        /// Valida datos JSON contra archivos PDF
        /// </summary>
        [HttpPost("validate-pdf")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ValidatePdf([FromForm] CrossValidationRequest request)
        {
            // Validaciones básicas
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("File is not provided.");
            }

            if (string.IsNullOrWhiteSpace(request.JsonData))
            {
                return BadRequest("JSON data is not provided.");
            }

            if (request.File.ContentType != "application/pdf")
            {
                return BadRequest("Invalid file type. Only PDF is supported.");
            }

            try
            {
                Console.WriteLine("[INFO] Detectando proveedor de IA disponible...");
                // Detectar automáticamente el proveedor disponible
                var availableProvider = await DetectAvailableProvider();
                if (string.IsNullOrEmpty(availableProvider))
                {
                    Console.WriteLine("[ERROR] No hay proveedores de IA disponibles");
                    return StatusCode(500, "No AI provider is available. Please configure OPENAI_API_KEY or GOOGLE_API_KEY environment variables.");
                }

                Console.WriteLine($"[INFO] Proveedor detectado: {availableProvider}");
                request.Provider = availableProvider;
                
                // Guardar archivo temporalmente
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                try
                {
                    Console.WriteLine($"[INFO] Iniciando extracción de información con {availableProvider}...");
                    // Extraer información de la imagen
                    var extractedInfo = await ExtractInformationFromImage(tempFilePath, availableProvider, request.DocumentType);
                    Console.WriteLine($"[INFO] Información extraída exitosamente");

                    Console.WriteLine($"[INFO] Iniciando comparación de datos...");
                    // Comparar con los datos JSON proporcionados
                    var result = await CompareDataWithAI(extractedInfo, request.JsonData, availableProvider, request.FieldsToValidate);
                    Console.WriteLine($"[INFO] Validación completada");

                    return Ok(result);
                }
                finally
                {
                    // Limpiar archivo temporal
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error durante la validación: {ex.Message}");
                return StatusCode(500, $"An error occurred during validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Valida datos JSON contra archivos de imagen
        /// </summary>
        [HttpPost("validate-image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ValidateImage([FromForm] CrossValidationRequest request)
        {
            // Validaciones básicas
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("File is not provided.");
            }

            if (string.IsNullOrWhiteSpace(request.JsonData))
            {
                return BadRequest("JSON data is not provided.");
            }

            // Validar tipos de imagen soportados
            var supportedImageTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/bmp", "image/webp" };
            if (!supportedImageTypes.Contains(request.File.ContentType?.ToLower()))
            {
                return BadRequest("Invalid file type. Supported image types: JPEG, PNG, GIF, BMP, WebP.");
            }

            try
            {
                Console.WriteLine("[INFO] Detectando proveedor de IA disponible...");
                // Detectar automáticamente el proveedor disponible
                var availableProvider = await DetectAvailableProvider();
                if (string.IsNullOrEmpty(availableProvider))
                {
                    Console.WriteLine("[ERROR] No hay proveedores de IA disponibles");
                    return StatusCode(500, "No AI provider is available. Please configure OPENAI_API_KEY or GOOGLE_API_KEY environment variables.");
                }

                Console.WriteLine($"[INFO] Proveedor detectado: {availableProvider}");
                request.Provider = availableProvider;
                
                // Guardar archivo temporalmente
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await request.File.CopyToAsync(stream);
                }

                try
                {
                    Console.WriteLine($"[INFO] Iniciando extracción de información con {availableProvider}...");
                    // Extraer información de la imagen directamente (sin conversión)
                    var extractedInfo = await ExtractInformationFromImage(tempFilePath, availableProvider, request.DocumentType);
                    Console.WriteLine($"[INFO] Información extraída exitosamente");

                    Console.WriteLine($"[INFO] Iniciando comparación de datos...");
                    // Comparar con los datos JSON proporcionados
                    var result = await CompareDataWithAI(extractedInfo, request.JsonData, availableProvider, request.FieldsToValidate);
                    Console.WriteLine($"[INFO] Validación completada");

                    return Ok(result);
                }
                finally
                {
                    // Limpiar archivo temporal
                    if (System.IO.File.Exists(tempFilePath))
                    {
                        System.IO.File.Delete(tempFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error durante la validación: {ex.Message}");
                return StatusCode(500, $"An error occurred during validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Extrae información estructurada de una imagen usando IA
        /// </summary>
        private async Task<string> ExtractInformationFromImage(string imagePath, string provider, string? documentType)
        {
            Console.WriteLine($"[INFO] Iniciando extracción de información con {provider}...");
            Console.WriteLine($"[INFO] Tipo de documento: {documentType ?? "no especificado"}");
            string prompt = CreateExtractionPrompt(documentType);
            Console.WriteLine($"[INFO] Prompt de extracción creado, longitud: {prompt.Length} caracteres");

            string processedImagePath = imagePath;
            
            // Si es OpenAI y el archivo parece ser PDF, convertirlo a imagen
            if (provider.ToLower() == "openai")
            {
                // Verificar si el archivo es PDF leyendo los primeros bytes
                var fileBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
                if (fileBytes.Length > 4 && fileBytes[0] == 0x25 && fileBytes[1] == 0x50 && fileBytes[2] == 0x44 && fileBytes[3] == 0x46) // %PDF
                {
                    Console.WriteLine("[INFO] Archivo PDF detectado, convirtiendo a imagen para OpenAI...");
                    processedImagePath = await ConvertPdfFileToImage(imagePath);
                }
                
                Console.WriteLine("[INFO] Usando OpenAI para extracción...");
                var result = await ExtractWithOpenAI(processedImagePath, prompt);
                
                // Limpiar archivo temporal si se creó uno
                if (processedImagePath != imagePath && System.IO.File.Exists(processedImagePath))
                {
                    System.IO.File.Delete(processedImagePath);
                }
                
                return result;
            }
            else
            {
                Console.WriteLine("[INFO] Usando Gemini para extracción...");
                return await ExtractWithGemini(imagePath, prompt);
            }
        }

        /// <summary>
        /// Crea el prompt para extracción de información según el tipo de documento
        /// </summary>
        private string CreateExtractionPrompt(string? documentType)
        {
            string basePrompt = "Analiza esta imagen y extrae toda la información importante en formato JSON estructurado. ";

            return documentType?.ToLower() switch
            {
                "certificado" => basePrompt + "Este es un certificado. Extrae: nombre del titular, institución emisora, fecha de emisión, fecha de vencimiento, número de certificado, tipo de certificación, y cualquier otra información relevante.",
                "factura" => basePrompt + "Esta es una factura. Extrae: número de factura, fecha, proveedor, cliente, items/productos, cantidades, precios, subtotal, impuestos, total, y términos de pago.",
                "contrato" => basePrompt + "Este es un contrato. Extrae: partes involucradas, fecha del contrato, objeto del contrato, duración, términos principales, firmas, y fechas importantes.",
                "identificacion" => basePrompt + "Este es un documento de identificación. Extrae: nombre completo, número de documento, fecha de nacimiento, fecha de emisión, fecha de vencimiento, lugar de nacimiento, y otros datos personales.",
                "diploma" => basePrompt + "Este es un diploma. Extrae: nombre del graduado, institución, título/grado obtenido, fecha de graduación, firmas de autoridades, y sellos oficiales.",
                _ => basePrompt + "Extrae toda la información textual visible, incluyendo nombres, fechas, números, direcciones, y cualquier dato estructurado que encuentres."
            };
        }

        /// <summary>
        /// Extrae información usando Gemini
        /// </summary>
        private async Task<string> ExtractWithGemini(string imagePath, string prompt)
        {
            Console.WriteLine("[INFO] Iniciando extracción con Gemini...");
            var apiKey = _configuration["AI:Google:ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("[ERROR] La clave de API de Google no está configurada.");
                throw new InvalidOperationException("La clave de API de Google no está configurada.");
            }

            Console.WriteLine("[INFO] Creando modelo Gemini...");
            var model = new GenerativeModel(apiKey, "gemma-3");

            Console.WriteLine($"[INFO] Leyendo imagen desde: {imagePath}");
            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
            Console.WriteLine($"[INFO] Imagen leída, tamaño: {imageBytes.Length} bytes");
            
            var content = new Content();
            content.AddText(prompt);
            content.AddInlineFile("image/png", Convert.ToBase64String(imageBytes));

            var request = new GenerateContentRequest();
            request.Contents.Add(content);

            Console.WriteLine("[INFO] Enviando solicitud a Gemini...");
            var response = await model.GenerateContentAsync(request);
            Console.WriteLine("[INFO] Respuesta recibida de Gemini");
            return response.Text() ?? "No se pudo extraer información.";
        }

        /// <summary>
        /// Extrae información usando OpenAI
        /// </summary>
        private async Task<string> ExtractWithOpenAI(string imagePath, string prompt)
        {
            Console.WriteLine("[DEBUG] Iniciando ExtractWithOpenAI");

            var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            Console.WriteLine($"[DEBUG] OPENAI_API_KEY encontrada: {!string.IsNullOrEmpty(apiKey)}");
            Console.WriteLine($"[DEBUG] Longitud de la clave: {apiKey?.Length ?? 0}");

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("[ERROR] La clave de API de OpenAI no está configurada en ExtractWithOpenAI.");
                Console.WriteLine("[DEBUG] Variables de entorno disponibles:");
                foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
                {
                    if (env.Key.ToString().Contains("OPENAI", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[DEBUG] {env.Key}: {env.Value}");
                    }
                }
                throw new InvalidOperationException("La clave de API de OpenAI no está configurada.");
            }

            Console.WriteLine("[DEBUG] Creando cliente OpenAI para extracción");
            var openAiClient = new OpenAIClient(apiKey);
            var chatClient = openAiClient.GetChatClient("gpt-4o");

            Console.WriteLine($"[DEBUG] Leyendo imagen desde: {imagePath}");
            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
            Console.WriteLine($"[DEBUG] Imagen leída, tamaño: {imageBytes.Length} bytes");

            var messages = new List<ChatMessage>
             {
                 new UserChatMessage(
                     ChatMessageContentPart.CreateTextPart(prompt),
                     ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), "image/png")
                 )
             };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 1500
            };

            Console.WriteLine("[DEBUG] Enviando solicitud extracción a OpenAI");
            var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            Console.WriteLine("[DEBUG] Respuesta de extracción recibida de OpenAI");
            return response.Value.Content[0].Text;
        }

        /// <summary>
        /// Compara los datos JSON con la información extraída usando IA
        /// </summary>
        private async Task<CrossValidationResponse> CompareDataWithAI(string extractedInfo, string jsonData, string provider, List<string>? fieldsToValidate)
        {
            Console.WriteLine($"[INFO] Iniciando comparación de datos con {provider}...");
            string comparisonPrompt = CreateComparisonPrompt(extractedInfo, jsonData, fieldsToValidate);
            Console.WriteLine($"[INFO] Prompt de comparación creado, longitud: {comparisonPrompt.Length} caracteres");

            string aiResponse;
            if (provider.ToLower() == "openai")
            {
                Console.WriteLine("[INFO] Usando OpenAI para comparación...");
                aiResponse = await GetComparisonFromOpenAI(comparisonPrompt);
            }
            else
            {
                Console.WriteLine("[INFO] Usando Gemini para comparación...");
                aiResponse = await GetComparisonFromGemini(comparisonPrompt);
            }

            Console.WriteLine("[INFO] Parseando respuesta de validación...");
            var response = ParseValidationResponse(aiResponse, provider);
            response.ExtractedData = extractedInfo;
            response.ProvidedData = jsonData;
            response.AIResponse = aiResponse;
            Console.WriteLine($"[INFO] Comparación completada. Resultado válido: {response.IsValid}");
            return response;
        }

        /// <summary>
        /// Crea el prompt para comparación de datos
        /// </summary>
        private string CreateComparisonPrompt(string extractedInfo, string jsonData, List<string>? fieldsToValidate)
        {
            var prompt = $@"
             Eres un experto validador de documentos. Tu tarea es comparar la información extraída de un documento con los datos proporcionados en JSON.
             
             INFORMACIÓN EXTRAÍDA DEL DOCUMENTO:
             {extractedInfo}
             
             DATOS A VALIDAR (JSON):
             {jsonData}
             
             INSTRUCCIONES:
             1. Compara cada campo del JSON con la información extraída del documento
             2. Determina si los datos coinciden, son similares, o son diferentes
             3. Asigna un nivel de confianza (0-100) para cada campo
             4. Proporciona una validación general (true/false)
             
             RESPONDE EN EL SIGUIENTE FORMATO JSON:
             {{
                 ""isValid"": true/false,
                 ""overallConfidence"": 0-100,
                 ""fieldValidations"": [
                     {{
                         ""fieldName"": ""nombre_del_campo"",
                         ""isValid"": true/false,
                         ""confidence"": 0-100,
                         ""extractedValue"": ""valor_extraído"",
                         ""providedValue"": ""valor_proporcionado"",
                         ""notes"": ""observaciones_si_las_hay""
                     }}
                 ]
             }}";

            if (fieldsToValidate?.Any() == true)
            {
                prompt += $"\n\nVALIDA ÚNICAMENTE ESTOS CAMPOS: {string.Join(", ", fieldsToValidate)}";
            }

            return prompt;
        }

        /// <summary>
        /// Obtiene comparación usando OpenAI
        /// </summary>
        private async Task<string> GetComparisonFromOpenAI(string prompt)
        {
            Console.WriteLine("[DEBUG] Iniciando GetComparisonFromOpenAI");

            var apiKey = _configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            Console.WriteLine($"[DEBUG] OPENAI_API_KEY encontrada: {!string.IsNullOrEmpty(apiKey)}");
            Console.WriteLine($"[DEBUG] Longitud de la clave: {apiKey?.Length ?? 0}");

            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("[ERROR] La clave de API de OpenAI no está configurada.");
                Console.WriteLine("[DEBUG] Variables de entorno disponibles:");
                foreach (DictionaryEntry env in Environment.GetEnvironmentVariables())
                {
                    if (env.Key.ToString().Contains("OPENAI", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[DEBUG] {env.Key}: {env.Value}");
                    }
                }
                throw new InvalidOperationException("La clave de API de OpenAI no está configurada.");
            }

            Console.WriteLine("[DEBUG] Creando cliente OpenAI");
            var openAiClient = new OpenAIClient(apiKey);
            var chatClient = openAiClient.GetChatClient("gpt-4");

            var messages = new List<ChatMessage>
             {
                 new UserChatMessage(prompt)
             };

            var chatCompletionOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 2000
            };

            Console.WriteLine("[DEBUG] Enviando solicitud a OpenAI");
            var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
            Console.WriteLine("[DEBUG] Respuesta recibida de OpenAI");
            return response.Value.Content[0].Text;
        }

        /// <summary>
        /// Obtiene comparación usando Gemini
        /// </summary>
        private async Task<string> GetComparisonFromGemini(string prompt)
        {
            Console.WriteLine("[INFO] Iniciando comparación con Gemini...");
            var apiKey = _configuration["AI:Google:ApiKey"] ?? Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("[ERROR] La clave de API de Google no está configurada.");
                throw new InvalidOperationException("La clave de API de Google no está configurada.");
            }

            Console.WriteLine("[INFO] Creando modelo Gemini para comparación...");
            var model = new GenerativeModel(apiKey, "gemini-1.5-pro");

            var content = new Content();
            content.AddText(prompt);

            var request = new GenerateContentRequest();
            request.Contents.Add(content);

            Console.WriteLine("[INFO] Enviando solicitud de comparación a Gemini...");
            var response = await model.GenerateContentAsync(request);
            Console.WriteLine("[INFO] Respuesta de comparación recibida de Gemini");
            return response.Text() ?? "No se pudo obtener respuesta de validación.";
        }

        /// <summary>
        /// Parsea la respuesta de IA y crea el objeto de respuesta
        /// </summary>
        private CrossValidationResponse ParseValidationResponse(string aiResponse, string provider)
        {
            try
            {
                // Intentar extraer JSON de la respuesta
                var jsonStart = aiResponse.IndexOf('{');
                var jsonEnd = aiResponse.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var validationResult = JsonSerializer.Deserialize<JsonElement>(jsonContent);

                    var response = new CrossValidationResponse
                    {
                        IsValid = validationResult.GetProperty("isValid").GetBoolean(),
                        ConfidenceScore = validationResult.GetProperty("overallConfidence").GetInt32(),
                        Provider = provider,
                        FieldValidations = new List<FieldValidationResult>()
                    };

                    if (validationResult.TryGetProperty("fieldValidations", out var fieldsArray))
                    {
                        foreach (var field in fieldsArray.EnumerateArray())
                        {
                            response.FieldValidations.Add(new FieldValidationResult
                            {
                                FieldName = field.GetProperty("fieldName").GetString() ?? "",
                                IsMatch = field.GetProperty("isValid").GetBoolean(),
                                ConfidenceScore = field.GetProperty("confidence").GetInt32(),
                                ExtractedValue = field.TryGetProperty("extractedValue", out var extracted) ? extracted.GetString() : null,
                                ProvidedValue = field.TryGetProperty("providedValue", out var provided) ? provided.GetString() : null,
                                Notes = field.TryGetProperty("notes", out var notes) ? notes.GetString() : null
                            });
                        }
                    }

                    return response;
                }
            }
            catch (Exception ex)
            {
                // Si falla el parsing, crear respuesta de error
                return new CrossValidationResponse
                {
                    IsValid = false,
                    ConfidenceScore = 0,
                    Provider = provider,
                    FieldValidations = new List<FieldValidationResult>
                     {
                         new FieldValidationResult
                         {
                             FieldName = "parsing_error",
                             IsMatch = false,
                             ConfidenceScore = 0,
                             Notes = $"Error al procesar respuesta de IA: {ex.Message}"
                         }
                     }
                };
            }

            // Respuesta de fallback
            return new CrossValidationResponse
            {
                IsValid = false,
                ConfidenceScore = 0,
                Provider = provider,
                FieldValidations = new List<FieldValidationResult>
                 {
                     new FieldValidationResult
                     {
                         FieldName = "unknown_error",
                         IsMatch = false,
                         ConfidenceScore = 0,
                         Notes = "No se pudo procesar la respuesta de validación"
                     }
                 }
            };
        }
    }
}

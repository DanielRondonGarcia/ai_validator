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
        /// <summary>
        /// Constructor for ValidationController.
        /// </summary>
        public ValidationController()
        {
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
            // Validaciones básicas
            if (request.File == null || request.File.Length == 0)
            {
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(request.JsonData))
            {
                return BadRequest("JSON data is not provided.");
            }

            if (request.File.ContentType != "application/pdf")
            {
                return BadRequest("Invalid file type. Only PDF is supported.");
            }

            try
            {
                // Detectar automáticamente el proveedor disponible
                var availableProvider = await DetectAvailableProvider();
                if (string.IsNullOrEmpty(availableProvider))
                {
                    return StatusCode(500, "No AI provider is available. Please configure OPENAI_API_KEY or GOOGLE_API_KEY environment variables.");
                }

                return availableProvider switch
                {
                    "openai" => await CrossValidateOpenAI(request),
                    "gemini" => await CrossValidateGemini(request),
                    _ => StatusCode(500, "Unable to determine available AI provider.")
                };
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred during cross-validation: {ex.Message}");
            }
        }

        /// <summary>
        /// Detecta qué proveedor de IA está disponible y funcional
        /// </summary>
        /// <returns>El nombre del proveedor disponible o null si ninguno está disponible</returns>
        private async Task<string?> DetectAvailableProvider()
        {
            // Verificar OpenAI primero (generalmente más rápido)
            if (await IsOpenAIAvailable())
            {
                return "openai";
            }

            // Verificar Gemini como alternativa
            if (await IsGeminiAvailable())
            {
                return "gemini";
            }

            return null;
        }

        /// <summary>
        /// Verifica si OpenAI está disponible
        /// </summary>
        private async Task<bool> IsOpenAIAvailable()
        {
            try
            {
                var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (string.IsNullOrEmpty(apiKey))
                {
                    return false;
                }

                // Hacer una prueba rápida con OpenAI
                var openAiClient = new OpenAIClient(apiKey);
                var chatClient = openAiClient.GetChatClient("gpt-4o");

                var testMessages = new List<ChatMessage>
                {
                    new UserChatMessage("Test")
                };

                var testOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 1
                };

                await chatClient.CompleteChatAsync(testMessages, testOptions);
                return true;
            }
            catch
            {
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
                var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
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
        /// Endpoint inteligente que analiza documentos PDF detectando automáticamente el proveedor de IA disponible.
        /// </summary>
        /// <remarks>
        /// Sube un archivo PDF y una pregunta o prompt en lenguaje natural.
        /// La API detectará automáticamente qué proveedor de IA está disponible (OpenAI o Google Gemini)
        /// y utilizará el que esté funcional para procesar tu solicitud.
        /// 
        /// **Detección automática:** El sistema verifica primero OpenAI y luego Gemini.
        /// **Variables de entorno requeridas:** `OPENAI_API_KEY` y/o `GOOGLE_API_KEY`.
        ///
        /// Ejemplo de prompt:
        ///
        ///     "¿Este documento es un certificado de finalización para el curso de .NET? ¿A nombre de quién está?"
        ///
        /// </remarks>
        /// <param name="file">El archivo PDF a validar.</param>
        /// <param name="schema">La pregunta o prompt en lenguaje natural para la IA.</param>
        /// <returns>La respuesta de texto generada por el modelo de IA disponible.</returns>
        /// <response code="200">Devuelve la respuesta de la IA con información del proveedor utilizado.</response>
        /// <response code="400">Si el archivo o el prompt no se proporcionan, o si no hay proveedores disponibles.</response>
        /// <response code="500">Si ocurre un error interno durante el procesamiento.</response>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateIntelligent(IFormFile file, [FromForm] string schema)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(schema))
            {
                return BadRequest("Prompt (schema) is not provided.");
            }

            if (file.ContentType != "application/pdf")
            {
                return BadRequest("Invalid file type. Only PDF is supported for now.");
            }

            // Detectar proveedor disponible
            var availableProvider = await DetectAvailableProvider();
            if (string.IsNullOrEmpty(availableProvider))
            {
                return BadRequest("No AI providers are available. Please configure OPENAI_API_KEY or GOOGLE_API_KEY environment variables.");
            }

            string? tempFilePath = null;
            try
            {
                // 1. Convert PDF page to an image
                tempFilePath = await ConvertPdfToImage(file);

                // 2. Call the available AI provider
                string aiResponse;
                string providerName;

                if (availableProvider == "openai")
                {
                    aiResponse = await ProcessWithOpenAI(tempFilePath, schema);
                    providerName = "OpenAI Vision";
                }
                else
                {
                    aiResponse = await ProcessWithGemini(tempFilePath, schema);
                    providerName = "Google Gemini";
                }

                return Ok(new
                {
                    FileName = file.FileName,
                    Prompt = schema,
                    Provider = providerName,
                    AI_Response = aiResponse,
                    AutoDetected = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred while processing the file with {availableProvider}: {ex.Message}");
            }
            finally
            {
                if (tempFilePath != null && System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
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
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            var openAiClient = new OpenAIClient(apiKey!);
            var chatClient = openAiClient.GetChatClient("gpt-4o");

            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);

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

            var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
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
            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
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
        /// Valida datos JSON contra PDF/imagen usando Gemini específicamente
        /// </summary>
        [HttpPost("cross-validate-gemini")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CrossValidateGemini([FromForm] CrossValidationRequest request)
        {
            request.Provider = "gemini";
            return await CrossValidate(request);
        }

        /// <summary>
        /// Valida datos JSON contra PDF/imagen usando OpenAI específicamente
        /// </summary>
        [HttpPost("cross-validate-openai")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> CrossValidateOpenAI([FromForm] CrossValidationRequest request)
        {
            request.Provider = "openai";
            return await CrossValidate(request);
        }

        /// <summary>
        /// Extrae información estructurada de una imagen usando IA
        /// </summary>
        private async Task<string> ExtractInformationFromImage(string imagePath, string provider, string? documentType)
        {
            string prompt = CreateExtractionPrompt(documentType);

            if (provider.ToLower() == "openai")
            {
                return await ExtractWithOpenAI(imagePath, prompt);
            }
            else
            {
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
            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("La clave de API de Google no está configurada.");
            }

            var model = new GenerativeModel(apiKey, "gemma-3");

            var imageBytes = await System.IO.File.ReadAllBytesAsync(imagePath);
            var content = new Content();
            content.AddText(prompt);
            content.AddInlineFile("image/png", Convert.ToBase64String(imageBytes));

            var request = new GenerateContentRequest();
            request.Contents.Add(content);

            var response = await model.GenerateContentAsync(request);
            return response.Text() ?? "No se pudo extraer información.";
        }

        /// <summary>
        /// Extrae información usando OpenAI
        /// </summary>
        private async Task<string> ExtractWithOpenAI(string imagePath, string prompt)
        {
            Console.WriteLine("[DEBUG] Iniciando ExtractWithOpenAI");

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
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
            string comparisonPrompt = CreateComparisonPrompt(extractedInfo, jsonData, fieldsToValidate);

            string aiResponse;
            if (provider.ToLower() == "openai")
            {
                aiResponse = await GetComparisonFromOpenAI(comparisonPrompt);
            }
            else
            {
                aiResponse = await GetComparisonFromGemini(comparisonPrompt);
            }

            var response = ParseValidationResponse(aiResponse, provider);
            response.ExtractedData = extractedInfo;
            response.ProvidedData = jsonData;
            response.AIResponse = aiResponse;
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

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
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
            var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("La clave de API de Google no está configurada.");
            }

            var model = new GenerativeModel(apiKey, "gemini-1.5-pro");

            var content = new Content();
            content.AddText(prompt);

            var request = new GenerateContentRequest();
            request.Contents.Add(content);

            var response = await model.GenerateContentAsync(request);
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

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
    /// Controlador para la validación de documentos usando Google Gemini AI y OpenAI Vision.
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
        [HttpPost("validate-gemini")]
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
                tempFilePath = await ConvertPdfToImage(file);

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
                    Provider = "Google Gemini",
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

        /// <summary>
        /// Analiza el contenido de un archivo PDF usando OpenAI Vision (GPT-4 Vision).
        /// </summary>
        /// <remarks>
        /// Sube un archivo PDF y una pregunta o prompt en lenguaje natural.
        /// La API convertirá la primera página del PDF en una imagen y la enviará a OpenAI Vision
        /// junto con tu pregunta en una única petición multimodal.
        /// **Importante:** Se requiere una clave de API de OpenAI. La aplicación la buscará
        /// en la variable de entorno `OPENAI_API_KEY`.
        ///
        /// Ejemplo de prompt:
        ///
        ///     "¿Este documento es un certificado de finalización para el curso de .NET? ¿A nombre de quién está?"
        ///
        /// </remarks>
        /// <param name="file">El archivo PDF a validar.</param>
        /// <param name="schema">La pregunta o prompt en lenguaje natural para OpenAI.</param>
        /// <returns>La respuesta de texto generada por el modelo de IA.</returns>
        /// <response code="200">Devuelve la respuesta de la IA.</response>
        /// <response code="400">Si el archivo, el prompt o la clave de API no se proporcionan.</response>
        /// <response code="500">Si ocurre un error interno durante el procesamiento.</response>
        [HttpPost("validate-openai")]
        public async Task<IActionResult> ValidateWithOpenAI(IFormFile file, [FromForm] string schema)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File is not provided or empty.");
            }

            if (string.IsNullOrEmpty(schema))
            {
                return BadRequest("Prompt (schema) is not provided.");
            }

            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                return BadRequest("OpenAI API Key is not configured. Please set the OPENAI_API_KEY environment variable.");
            }

            if (file.ContentType != "application/pdf")
            {
                return BadRequest("Invalid file type. Only PDF is supported for now.");
            }

            string? tempFilePath = null;
            try
            {
                // 1. Convert PDF page to an in-memory image using IronPDF
                tempFilePath = await ConvertPdfToImage(file);

                // 2. Call OpenAI Vision API
                var openAiClient = new OpenAIClient(apiKey);
                var chatClient = openAiClient.GetChatClient("gpt-4o");
                
                // Convert image to base64
                var imageBytes = await System.IO.File.ReadAllBytesAsync(tempFilePath);
                var base64Image = Convert.ToBase64String(imageBytes);
                var imageUrl = $"data:image/png;base64,{base64Image}";

                var messages = new List<ChatMessage>
                {
                    new UserChatMessage(
                        ChatMessageContentPart.CreateTextPart(schema),
                        ChatMessageContentPart.CreateImagePart(BinaryData.FromBytes(imageBytes), "image/png")
                    )
                };

                var chatCompletionOptions = new ChatCompletionOptions
                {
                    MaxOutputTokenCount = 1000
                };

                var response = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);

                return Ok(new {
                    FileName = file.FileName,
                    Prompt = schema,
                    Provider = "OpenAI Vision",
                    AI_Response = response.Value.Content[0].Text
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

        /// <summary>
        /// Endpoint unificado que permite elegir el proveedor de IA (Google Gemini u OpenAI Vision).
        /// </summary>
        /// <param name="file">El archivo PDF a validar.</param>
        /// <param name="schema">La pregunta o prompt en lenguaje natural.</param>
        /// <param name="provider">El proveedor de IA a usar: 'gemini' o 'openai'. Por defecto es 'gemini'.</param>
        /// <returns>La respuesta de texto generada por el modelo de IA seleccionado.</returns>
        [HttpPost("validate")]
        public async Task<IActionResult> ValidateUnified(IFormFile file, [FromForm] string schema, [FromForm] string provider = "gemini")
        {
            provider = provider?.ToLowerInvariant() ?? "gemini";
            
            return provider switch
            {
                "openai" => await ValidateWithOpenAI(file, schema),
                "gemini" => await Validate(file, schema),
                _ => BadRequest("Invalid provider. Use 'gemini' or 'openai'.")
            };
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
        /// Valida datos JSON contra el contenido de un PDF o imagen usando IA
        /// </summary>
        /// <param name="request">Solicitud de validación cruzada</param>
        /// <returns>Resultado de validación con detalles</returns>
        [HttpPost("cross-validate")]
        public async Task<IActionResult> CrossValidate([FromForm] CrossValidationRequest request)
        {
            try
            {
                // 1. Validar entrada
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { Error = "No se proporcionó ningún archivo." });
                }

                if (string.IsNullOrWhiteSpace(request.JsonData))
                {
                    return BadRequest(new { Error = "No se proporcionaron datos JSON para validar." });
                }

                // 2. Validar JSON
                JsonElement providedData;
                try
                {
                    providedData = JsonSerializer.Deserialize<JsonElement>(request.JsonData);
                }
                catch (JsonException)
                {
                    return BadRequest(new { Error = "Los datos JSON proporcionados no son válidos." });
                }

                // 3. Convertir archivo a imagen
                string tempFilePath = await ConvertPdfToImage(request.File);

                // 4. Extraer información usando IA
                var extractedInfo = await ExtractInformationFromImage(tempFilePath, request.Provider, request.DocumentType);

                // 5. Comparar datos
                var validationResult = await CompareDataWithAI(extractedInfo, request.JsonData, request.Provider, request.FieldsToValidate);

                // 6. Limpiar archivo temporal
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }

                return Ok(validationResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = $"Error interno del servidor: {ex.Message}" });
            }
        }

        /// <summary>
        /// Valida datos JSON contra PDF/imagen usando Gemini específicamente
        /// </summary>
        [HttpPost("cross-validate-gemini")]
        public async Task<IActionResult> CrossValidateGemini([FromForm] IFormFile file, [FromForm] string jsonData, [FromForm] string? documentType = null)
        {
            var request = new CrossValidationRequest
            {
                File = file,
                JsonData = jsonData,
                Provider = "gemini",
                DocumentType = documentType
            };
            return await CrossValidate(request);
        }

        /// <summary>
        /// Valida datos JSON contra PDF/imagen usando OpenAI específicamente
        /// </summary>
        [HttpPost("cross-validate-openai")]
        public async Task<IActionResult> CrossValidateOpenAI([FromForm] IFormFile file, [FromForm] string jsonData, [FromForm] string? documentType = null)
        {
            var request = new CrossValidationRequest
            {
                File = file,
                JsonData = jsonData,
                Provider = "openai",
                DocumentType = documentType
            };
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

             var model = new GenerativeModel(apiKey, "gemini-1.5-pro-vision-latest");
             
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

             Console.WriteLine("[DEBUG] Enviando solicitud de extracción a OpenAI");
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

using AI.Library.Core;
using AI.Library.Extensions;
using AI.Library.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI.Library.Examples
{
    /// <summary>
    /// Ejemplo básico de uso de AI.Library
    /// </summary>
    public class BasicUsageExample
    {
        /// <summary>
        /// Ejemplo de configuración y uso básico de la librería
        /// </summary>
        public static async Task RunExampleAsync()
        {
            // Configurar servicios
            var services = new ServiceCollection();
            
            // Agregar logging
            services.AddLogging(builder => builder.AddConsole());
            
            // Configurar AI.Library con OpenAI
            services.AddAILibraryWithOpenAI(
                openAiApiKey: "tu-api-key-aqui", // En producción, usar variables de entorno
                model: "gpt-4o"
            );
            
            var serviceProvider = services.BuildServiceProvider();
            var aiService = serviceProvider.GetRequiredService<IAIService>();
            var logger = serviceProvider.GetRequiredService<ILogger<BasicUsageExample>>();
            
            logger.LogInformation("Iniciando ejemplos de AI.Library");
            
            // Ejemplo 1: Extracción de datos de imagen
            await ExtractDataFromImageExample(aiService, logger);
            
            // Ejemplo 2: Validación de datos
            await ValidateDataExample(aiService, logger);
            
            // Ejemplo 3: Traducción de texto
            await TranslateTextExample(aiService, logger);
            
            logger.LogInformation("Ejemplos completados");
        }
        
        /// <summary>
        /// Ejemplo de extracción de datos de una imagen
        /// </summary>
        private static async Task ExtractDataFromImageExample(IAIService aiService, ILogger logger)
        {
            logger.LogInformation("=== Ejemplo: Extracción de datos de imagen ===");
            
            try
            {
                // Simular datos de imagen (en un caso real, cargarías desde archivo)
                var imageData = await File.ReadAllBytesAsync("ruta/a/tu/imagen.jpg");
                
                var prompt = @"
                    Analiza esta imagen y extrae la siguiente información en formato JSON:
                    {
                        ""tipo_documento"": ""tipo de documento"",
                        ""fecha"": ""fecha encontrada"",
                        ""total"": ""monto total si aplica"",
                        ""texto_principal"": ""texto principal del documento""
                    }
                ";
                
                var resultado = await aiService.ExtractDataFromImageAsync(
                    imageData, 
                    "image/jpeg", 
                    prompt
                );
                
                if (resultado.Success)
                {
                    logger.LogInformation("Datos extraídos: {Data}", resultado.ExtractedData);
                    logger.LogInformation("Metadatos: {Metadata}", 
                        JsonSerializer.Serialize(resultado.Metadata, new JsonSerializerOptions { WriteIndented = true }));
                }
                else
                {
                    logger.LogError("Error en extracción: {Error}", resultado.ErrorMessage);
                }
            }
            catch (FileNotFoundException)
            {
                logger.LogWarning("Archivo de imagen no encontrado. Saltando ejemplo de extracción.");
            }
        }
        
        /// <summary>
        /// Ejemplo de validación de datos
        /// </summary>
        private static async Task ValidateDataExample(IAIService aiService, ILogger logger)
        {
            logger.LogInformation("=== Ejemplo: Validación de datos ===");
            
            var datosExtraidos = @"{
                ""numero_factura"": ""F-2024-001"",
                ""fecha"": ""2024-01-15"",
                ""total"": 1250.00,
                ""proveedor"": ""Empresa ABC""
            }";
            
            var referenciaJson = @"{
                ""numero_factura"": ""F-2024-001"",
                ""fecha"": ""2024-01-15"",
                ""total"": 1250.00,
                ""proveedor"": ""Empresa ABC"",
                ""estado"": ""pagado""
            }";
            
            var reglasValidacion = new[]
            {
                "El número de factura debe coincidir exactamente",
                "La fecha debe estar en formato YYYY-MM-DD",
                "El total debe ser un número positivo",
                "El proveedor debe estar presente"
            };
            
            var resultado = await aiService.ValidateDataAsync(
                datosExtraidos, 
                referenciaJson, 
                reglasValidacion
            );
            
            logger.LogInformation("Validación exitosa: {Success}", resultado.Success);
            logger.LogInformation("Datos válidos: {IsValid}", resultado.IsValid);
            logger.LogInformation("Confianza: {Confidence:P}", resultado.ConfidenceScore);
            
            if (resultado.Discrepancies?.Any() == true)
            {
                logger.LogInformation("Discrepancias encontradas:");
                foreach (var discrepancia in resultado.Discrepancies)
                {
                    logger.LogInformation("- {Discrepancy}", discrepancia);
                }
            }
        }
        
        /// <summary>
        /// Ejemplo de traducción de texto
        /// </summary>
        private static async Task TranslateTextExample(IAIService aiService, ILogger logger)
        {
            logger.LogInformation("=== Ejemplo: Traducción de texto ===");
            
            var textoOriginal = "Hola, ¿cómo estás? Espero que tengas un buen día.";
            var idiomaDestino = "inglés";
            
            var resultado = await aiService.TranslateTextAsync(
                textoOriginal, 
                idiomaDestino
            );
            
            if (resultado.Success)
            {
                logger.LogInformation("Texto original: {Original}", textoOriginal);
                logger.LogInformation("Texto traducido: {Translated}", resultado.TranslatedText);
                logger.LogInformation("Idioma detectado: {Detected}", resultado.DetectedSourceLanguage);
                logger.LogInformation("Idioma destino: {Target}", resultado.TargetLanguage);
                logger.LogInformation("Confianza: {Confidence:P}", resultado.ConfidenceScore);
                logger.LogInformation("Tiempo de procesamiento: {Time}ms", resultado.ProcessingTimeMs);
            }
            else
            {
                logger.LogError("Error en traducción: {Error}", resultado.ErrorMessage);
            }
        }
    }
    
    /// <summary>
    /// Ejemplo de uso con configuración desde appsettings.json
    /// </summary>
    public class ConfigurationExample
    {
        /// <summary>
        /// Ejemplo usando configuración desde archivo
        /// </summary>
        public static async Task RunWithConfigurationAsync()
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    // Configurar AI.Library usando appsettings.json
                    services.AddAILibrary(context.Configuration);
                })
                .Build();
            
            var aiService = host.Services.GetRequiredService<IAIService>();
            var logger = host.Services.GetRequiredService<ILogger<ConfigurationExample>>();
            
            // Usar el servicio...
            logger.LogInformation("AI.Library configurado desde appsettings.json");
            
            // Ejemplo de uso
            var resultado = await aiService.TranslateTextAsync(
                "Hello, how are you?", 
                "español"
            );
            
            if (resultado.Success)
            {
                logger.LogInformation("Traducción: {Translation}", resultado.TranslatedText);
            }
        }
    }
    
    /// <summary>
    /// Ejemplo de procesamiento de PDF
    /// </summary>
    public class PdfProcessingExample
    {
        /// <summary>
        /// Ejemplo de extracción de datos de un PDF
        /// </summary>
        public static async Task ProcessPdfExample(IAIService aiService, ILogger logger)
        {
            logger.LogInformation("=== Ejemplo: Procesamiento de PDF ===");
            
            try
            {
                // Cargar PDF desde archivo
                var pdfData = await File.ReadAllBytesAsync("ruta/a/tu/documento.pdf");
                
                var prompt = @"
                    Extrae todos los nombres, fechas y números de teléfono 
                    que encuentres en este documento. 
                    Organiza la información en formato JSON.
                ";
                
                var resultado = await aiService.ExtractDataFromPdfAsync(pdfData, prompt);
                
                if (resultado.Success)
                {
                    logger.LogInformation("Datos extraídos del PDF:");
                    logger.LogInformation("{Data}", resultado.ExtractedData);
                    
                    if (resultado.Metadata != null)
                    {
                        logger.LogInformation("Metadatos del procesamiento:");
                        foreach (var metadata in resultado.Metadata)
                        {
                            logger.LogInformation("- {Key}: {Value}", metadata.Key, metadata.Value);
                        }
                    }
                }
                else
                {
                    logger.LogError("Error procesando PDF: {Error}", resultado.ErrorMessage);
                }
            }
            catch (FileNotFoundException)
            {
                logger.LogWarning("Archivo PDF no encontrado. Saltando ejemplo de PDF.");
            }
        }
    }
}
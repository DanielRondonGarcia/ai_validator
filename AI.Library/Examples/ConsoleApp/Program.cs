using AI.Library.Core;
using AI.Library.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI.Library.Examples.ConsoleApp
{
    /// <summary>
    /// Aplicación de consola de ejemplo que demuestra el uso de AI.Library
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== AI.Library - Aplicación de Ejemplo ===");
            Console.WriteLine();

            // Configurar el host con dependency injection
            var host = CreateHostBuilder(args).Build();

            try
            {
                // Obtener servicios
                var aiService = host.Services.GetRequiredService<IAIService>();
                var logger = host.Services.GetRequiredService<ILogger<Program>>();

                logger.LogInformation("Iniciando aplicación de ejemplo");

                // Mostrar menú de opciones
                await ShowMenuAsync(aiService, logger);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("Presiona cualquier tecla para salir...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Configura el host builder con servicios y configuración
        /// </summary>
        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true)
                          .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                          .AddEnvironmentVariables()
                          .AddCommandLine(args);
                })
                .ConfigureServices((context, services) =>
                {
                    // Configurar AI.Library
                    services.AddAILibrary(context.Configuration);
                    
                    // Configurar logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.AddDebug();
                    });
                });

        /// <summary>
        /// Muestra el menú interactivo de opciones
        /// </summary>
        static async Task ShowMenuAsync(IAIService aiService, ILogger logger)
        {
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("=== Menú de Opciones ===");
                Console.WriteLine("1. Extraer datos de imagen");
                Console.WriteLine("2. Procesar PDF");
                Console.WriteLine("3. Validar datos JSON");
                Console.WriteLine("4. Traducir texto");
                Console.WriteLine("5. Ejemplo completo");
                Console.WriteLine("0. Salir");
                Console.WriteLine();
                Console.Write("Selecciona una opción: ");

                var input = Console.ReadLine();
                Console.WriteLine();

                try
                {
                    switch (input)
                    {
                        case "1":
                            await ExtractFromImageMenuAsync(aiService, logger);
                            break;
                        case "2":
                            await ProcessPdfMenuAsync(aiService, logger);
                            break;
                        case "3":
                            await ValidateDataMenuAsync(aiService, logger);
                            break;
                        case "4":
                            await TranslateTextMenuAsync(aiService, logger);
                            break;
                        case "5":
                            await RunCompleteExampleAsync(aiService, logger);
                            break;
                        case "0":
                            Console.WriteLine("¡Hasta luego!");
                            return;
                        default:
                            Console.WriteLine("Opción no válida. Intenta de nuevo.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error ejecutando opción {Option}", input);
                    Console.WriteLine($"Error: {ex.Message}");
                }

                Console.WriteLine();
                Console.WriteLine("Presiona cualquier tecla para continuar...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Menú para extracción de datos de imagen
        /// </summary>
        static async Task ExtractFromImageMenuAsync(IAIService aiService, ILogger logger)
        {
            Console.WriteLine("=== Extracción de Datos de Imagen ===");
            Console.Write("Ingresa la ruta de la imagen: ");
            var imagePath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                Console.WriteLine("Archivo no encontrado.");
                return;
            }

            Console.Write("Ingresa el prompt de extracción (o presiona Enter para usar el predeterminado): ");
            var prompt = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = "Extrae toda la información relevante de esta imagen en formato JSON estructurado.";
            }

            try
            {
                var imageData = await File.ReadAllBytesAsync(imagePath);
                var mimeType = GetMimeType(imagePath);

                Console.WriteLine("Procesando imagen...");
                var result = await aiService.ExtractDataFromImageAsync(imageData, mimeType, prompt);

                if (result.Success)
                {
                    Console.WriteLine("✅ Extracción exitosa:");
                    Console.WriteLine(result.ExtractedData);
                    
                    if (result.Metadata?.Any() == true)
                    {
                        Console.WriteLine("\nMetadatos:");
                        foreach (var meta in result.Metadata)
                        {
                            Console.WriteLine($"  {meta.Key}: {meta.Value}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error procesando imagen");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Menú para procesamiento de PDF
        /// </summary>
        static async Task ProcessPdfMenuAsync(IAIService aiService, ILogger logger)
        {
            Console.WriteLine("=== Procesamiento de PDF ===");
            Console.Write("Ingresa la ruta del PDF: ");
            var pdfPath = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                Console.WriteLine("Archivo no encontrado.");
                return;
            }

            Console.Write("Ingresa el prompt de extracción: ");
            var prompt = Console.ReadLine();
            
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = "Extrae toda la información importante de este documento.";
            }

            try
            {
                var pdfData = await File.ReadAllBytesAsync(pdfPath);

                Console.WriteLine("Procesando PDF...");
                var result = await aiService.ExtractDataFromPdfAsync(pdfData, prompt);

                if (result.Success)
                {
                    Console.WriteLine("✅ Extracción exitosa:");
                    Console.WriteLine(result.ExtractedData);
                }
                else
                {
                    Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error procesando PDF");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Menú para validación de datos
        /// </summary>
        static async Task ValidateDataMenuAsync(IAIService aiService, ILogger logger)
        {
            Console.WriteLine("=== Validación de Datos ===");
            
            Console.WriteLine("Ingresa los datos extraídos (JSON):");
            var extractedData = Console.ReadLine();
            
            Console.WriteLine("Ingresa los datos de referencia (JSON):");
            var referenceData = Console.ReadLine();
            
            Console.WriteLine("Ingresa las reglas de validación (separadas por ';'):");
            var rulesInput = Console.ReadLine();
            var rules = rulesInput?.Split(';', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();

            try
            {
                Console.WriteLine("Validando datos...");
                var validationResult = await aiService.ValidateDataAsync(extractedData ?? "", referenceData ?? "", rules);

                Console.WriteLine($"✅ Validación completada:");
                Console.WriteLine($"  Válido: {(validationResult.IsValid ? "Sí" : "No")}");
                Console.WriteLine($"  Confianza: {validationResult.ConfidenceScore:P}");
                
                if (validationResult.Discrepancies?.Any() == true)
                {
                    Console.WriteLine("  Discrepancias:");
                    foreach (var discrepancy in validationResult.Discrepancies)
                    {
                        Console.WriteLine($"    - {discrepancy}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error validando datos");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Menú para traducción de texto
        /// </summary>
        static async Task TranslateTextMenuAsync(IAIService aiService, ILogger logger)
        {
            Console.WriteLine("=== Traducción de Texto ===");
            
            Console.WriteLine("Ingresa el texto a traducir:");
            var text = Console.ReadLine();
            
            Console.Write("Ingresa el idioma destino: ");
            var targetLanguage = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetLanguage))
            {
                Console.WriteLine("Texto e idioma destino son requeridos.");
                return;
            }

            try
            {
                Console.WriteLine("Traduciendo...");
                var result = await aiService.TranslateTextAsync(text, targetLanguage);

                if (result.Success)
                {
                    Console.WriteLine("✅ Traducción exitosa:");
                    Console.WriteLine($"  Original: {text}");
                    Console.WriteLine($"  Traducido: {result.TranslatedText}");
                    Console.WriteLine($"  Idioma detectado: {result.DetectedSourceLanguage}");
                    Console.WriteLine($"  Confianza: {result.ConfidenceScore:P}");
                }
                else
                {
                    Console.WriteLine($"❌ Error: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error traduciendo texto");
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Ejecuta un ejemplo completo con datos de muestra
        /// </summary>
        static async Task RunCompleteExampleAsync(IAIService aiService, ILogger logger)
        {
            Console.WriteLine("=== Ejemplo Completo ===");
            Console.WriteLine("Ejecutando ejemplo con datos de muestra...");
            Console.WriteLine();

            // Ejemplo de traducción
            Console.WriteLine("1. Traduciendo texto...");
            var translationResult = await aiService.TranslateTextAsync(
                "Hello, how are you today?", 
                "español"
            );
            
            if (translationResult.Success)
            {
                Console.WriteLine($"   ✅ Traducción: {translationResult.TranslatedText}");
            }
            else
            {
                Console.WriteLine($"   ❌ Error: {translationResult.ErrorMessage}");
            }

            // Ejemplo de validación
            Console.WriteLine("\n2. Validando datos JSON...");
            var sampleData = @"{
                ""nombre"": ""Juan Pérez"",
                ""edad"": 30,
                ""email"": ""juan@example.com""
            }";
            
            var referenceData = @"{
                ""nombre"": ""Juan Pérez"",
                ""edad"": 30,
                ""email"": ""juan@example.com"",
                ""telefono"": ""123-456-7890""
            }";
            
            var validationRules = new[] 
            {
                "El nombre debe estar presente",
                "La edad debe ser un número positivo",
                "El email debe tener formato válido"
            };
            
            var validationResult = await aiService.ValidateDataAsync(
                sampleData, 
                referenceData, 
                validationRules
            );
            
            Console.WriteLine($"   ✅ Datos válidos: {validationResult.IsValid}");
            Console.WriteLine($"   ✅ Confianza: {validationResult.ConfidenceScore:P}");
            
            if (validationResult.Discrepancies?.Any() == true)
            {
                Console.WriteLine("   📋 Discrepancias encontradas:");
                foreach (var discrepancy in validationResult.Discrepancies)
                {
                    Console.WriteLine($"      - {discrepancy}");
                }
            }

            Console.WriteLine("\n✅ Ejemplo completo finalizado.");
        }

        /// <summary>
        /// Obtiene el tipo MIME basado en la extensión del archivo
        /// </summary>
        static string GetMimeType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "image/jpeg" // Default
            };
        }
    }
}
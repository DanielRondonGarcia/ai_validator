using AI.Library.Core;
using AI.Library.Models;
using AI.Library.Ports;
using AI.Library.Processors;
using AI.Library.Providers.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AI.Library.Extensions
{
    /// <summary>
    /// Extensiones para facilitar el registro de servicios de AI.Library en el contenedor de DI
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registra todos los servicios de AI.Library en el contenedor de dependencias
        /// </summary>
        /// <param name="services">Colección de servicios</param>
        /// <param name="configuration">Configuración de la aplicación</param>
        /// <param name="configureOptions">Acción opcional para configurar las opciones de IA</param>
        /// <returns>La colección de servicios para encadenamiento</returns>
        public static IServiceCollection AddAILibrary(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<AIModelsConfiguration>? configureOptions = null)
        {
            // Configurar opciones
            var configSection = configuration.GetSection("AIModels");
            services.Configure<AIModelsConfiguration>(configSection);
            
            if (configureOptions != null)
            {
                services.Configure(configureOptions);
            }

            // Registrar HttpClient
            services.AddHttpClient();

            // Registrar adaptadores específicos
            services.AddTransient<OpenAiAnalysisAdapter>();
            services.AddTransient<GeminiAnalysisAdapter>();
            services.AddTransient<IPdfProcessor, PdfProcessorAdapter>();

            // Registrar factory para proveedores de análisis
            services.AddTransient<Func<string, IAiAnalysisProvider>>(serviceProvider => providerName =>
            {
                return providerName.ToLowerInvariant() switch
                {
                    "openai" => serviceProvider.GetRequiredService<OpenAiAnalysisAdapter>(),
                    "google" or "gemini" => serviceProvider.GetRequiredService<GeminiAnalysisAdapter>(),
                    _ => throw new ArgumentException($"Proveedor de análisis no soportado: {providerName}")
                };
            });

            // Registrar proveedor de análisis basado en configuración
            services.AddTransient<IAiAnalysisProvider>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AIModelsConfiguration>>();
                var factory = serviceProvider.GetRequiredService<Func<string, IAiAnalysisProvider>>();
                var providerName = options.Value.AnalysisModel?.Provider ?? "openai";
                return factory(providerName);
            });

            // Registrar proveedor de visión (por ahora usa el mismo que análisis)
            services.AddTransient<IAiVisionProvider>(serviceProvider =>
            {
                var analysisProvider = serviceProvider.GetRequiredService<IAiAnalysisProvider>();
                // Aquí podrías implementar un adaptador específico para visión si es necesario
                return analysisProvider as IAiVisionProvider ?? 
                       throw new InvalidOperationException("El proveedor de análisis debe implementar IAiVisionProvider");
            });

            // Registrar servicio principal
            services.AddTransient<IAIService, AIService>();

            return services;
        }

        /// <summary>
        /// Registra AI.Library con configuración mínima usando OpenAI
        /// </summary>
        /// <param name="services">Colección de servicios</param>
        /// <param name="openAiApiKey">Clave API de OpenAI</param>
        /// <param name="model">Modelo a usar (por defecto gpt-4o)</param>
        /// <returns>La colección de servicios para encadenamiento</returns>
        public static IServiceCollection AddAILibraryWithOpenAI(
            this IServiceCollection services,
            string openAiApiKey,
            string model = "gpt-4o")
        {
            services.Configure<AIModelsConfiguration>(options =>
            {
                options.AnalysisModel = new AIModelConfig
                {
                    Provider = "OpenAI",
                    Model = model,
                    ApiKey = openAiApiKey,
                    BaseUrl = "https://api.openai.com/v1/chat/completions",
                    Temperature = 0.1,
                    MaxTokens = 4000
                };
                options.VisionModel = options.AnalysisModel;
            });

            return AddAILibraryCore(services);
        }

        /// <summary>
        /// Registra AI.Library con configuración mínima usando Gemini
        /// </summary>
        /// <param name="services">Colección de servicios</param>
        /// <param name="geminiApiKey">Clave API de Gemini</param>
        /// <param name="model">Modelo a usar (por defecto gemini-1.5-flash)</param>
        /// <returns>La colección de servicios para encadenamiento</returns>
        public static IServiceCollection AddAILibraryWithGemini(
            this IServiceCollection services,
            string geminiApiKey,
            string model = "gemini-1.5-flash")
        {
            services.Configure<AIModelsConfiguration>(options =>
            {
                options.AnalysisModel = new AIModelConfig
                {
                    Provider = "Google",
                    Model = model,
                    ApiKey = geminiApiKey,
                    BaseUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
                    Temperature = 0.1,
                    MaxTokens = 4000
                };
                options.VisionModel = options.AnalysisModel;
            });

            return AddAILibraryCore(services);
        }

        private static IServiceCollection AddAILibraryCore(IServiceCollection services)
        {
            // Registrar HttpClient
            services.AddHttpClient();

            // Registrar adaptadores específicos
            services.AddTransient<OpenAiAnalysisAdapter>();
            services.AddTransient<GeminiAnalysisAdapter>();
            services.AddTransient<IPdfProcessor, PdfProcessorAdapter>();

            // Registrar factory para proveedores de análisis
            services.AddTransient<Func<string, IAiAnalysisProvider>>(serviceProvider => providerName =>
            {
                return providerName.ToLowerInvariant() switch
                {
                    "openai" => serviceProvider.GetRequiredService<OpenAiAnalysisAdapter>(),
                    "google" or "gemini" => serviceProvider.GetRequiredService<GeminiAnalysisAdapter>(),
                    _ => throw new ArgumentException($"Proveedor de análisis no soportado: {providerName}")
                };
            });

            // Registrar proveedor de análisis basado en configuración
            services.AddTransient<IAiAnalysisProvider>(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<AIModelsConfiguration>>();
                var factory = serviceProvider.GetRequiredService<Func<string, IAiAnalysisProvider>>();
                var providerName = options.Value.AnalysisModel?.Provider ?? "openai";
                return factory(providerName);
            });

            // Registrar proveedor de visión (por ahora usa el mismo que análisis)
            services.AddTransient<IAiVisionProvider>(serviceProvider =>
            {
                var analysisProvider = serviceProvider.GetRequiredService<IAiAnalysisProvider>();
                return analysisProvider as IAiVisionProvider ?? 
                       throw new InvalidOperationException("El proveedor de análisis debe implementar IAiVisionProvider");
            });

            // Registrar servicio principal
            services.AddTransient<IAIService, AIService>();

            return services;
        }
    }
}
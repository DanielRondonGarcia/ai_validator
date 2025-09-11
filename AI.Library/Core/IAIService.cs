using AI.Library.Models;

namespace AI.Library.Core
{
    /// <summary>
    /// Interfaz principal que define los servicios de IA disponibles en la librería
    /// </summary>
    public interface IAIService
    {
        /// <summary>
        /// Extrae datos de una imagen utilizando IA
        /// </summary>
        /// <param name="imageData">Datos binarios de la imagen</param>
        /// <param name="mimeType">Tipo MIME de la imagen</param>
        /// <param name="prompt">Prompt para guiar la extracción</param>
        /// <returns>Resultado de la extracción</returns>
        Task<VisionExtractionResult> ExtractDataFromImageAsync(byte[] imageData, string mimeType, string prompt);

        /// <summary>
        /// Extrae datos de un PDF utilizando IA
        /// </summary>
        /// <param name="pdfData">Datos binarios del PDF</param>
        /// <param name="prompt">Prompt para guiar la extracción</param>
        /// <returns>Resultado de la extracción</returns>
        Task<VisionExtractionResult> ExtractDataFromPdfAsync(byte[] pdfData, string prompt);

        /// <summary>
        /// Valida datos extraídos contra un JSON de referencia
        /// </summary>
        /// <param name="extractedData">Datos extraídos</param>
        /// <param name="referenceJson">JSON de referencia</param>
        /// <param name="validationRules">Reglas de validación específicas</param>
        /// <returns>Resultado del análisis de validación</returns>
        Task<ValidationAnalysisResult> ValidateDataAsync(string extractedData, string referenceJson, string[]? validationRules = null);

        /// <summary>
        /// Traduce texto utilizando IA
        /// </summary>
        /// <param name="text">Texto a traducir</param>
        /// <param name="targetLanguage">Idioma destino</param>
        /// <param name="sourceLanguage">Idioma origen (opcional, se detecta automáticamente)</param>
        /// <returns>Texto traducido</returns>
        Task<TranslationResult> TranslateTextAsync(string text, string targetLanguage, string? sourceLanguage = null);
    }
}
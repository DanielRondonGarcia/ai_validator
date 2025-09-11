namespace AI.Library.Models
{
    /// <summary>
    /// Resultado de una operación de traducción
    /// </summary>
    public class TranslationResult
    {
        /// <summary>
        /// Indica si la traducción fue exitosa
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Texto traducido
        /// </summary>
        public string TranslatedText { get; set; } = string.Empty;

        /// <summary>
        /// Idioma detectado del texto original
        /// </summary>
        public string DetectedSourceLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Idioma destino de la traducción
        /// </summary>
        public string TargetLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Nivel de confianza de la traducción (0-1)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Mensaje de error si la traducción falló
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Tiempo de procesamiento en milisegundos
        /// </summary>
        public long ProcessingTimeMs { get; set; }

        /// <summary>
        /// Metadatos adicionales de la traducción
        /// </summary>
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
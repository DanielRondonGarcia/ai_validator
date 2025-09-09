using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace DataValidatorApi.Models
{
    /// <summary>
    /// Modelo para solicitudes de validación cruzada
    /// </summary>
    public class CrossValidationRequest
    {
        /// <summary>
        /// Archivo PDF o imagen a validar
        /// </summary>
        [Required(ErrorMessage = "El archivo es requerido")]
        public IFormFile File { get; set; } = null!;

        /// <summary>
        /// Datos en formato JSON para comparar con el contenido del archivo
        /// </summary>
        [Required(ErrorMessage = "Los datos JSON son requeridos")]
        public string JsonData { get; set; } = string.Empty;

        /// <summary>
        /// Proveedor de IA a utilizar (gemini, openai)
        /// </summary>
        public string Provider { get; set; } = "gemini";

        /// <summary>
        /// Tipo de documento para contexto adicional (certificado, factura, contrato, etc.)
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Campos específicos a validar (opcional)
        /// </summary>
        public List<string>? FieldsToValidate { get; set; }
    }

    /// <summary>
    /// Respuesta de validación cruzada
    /// </summary>
    public class CrossValidationResponse
    {
        /// <summary>
        /// Nombre del archivo procesado
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// Proveedor de IA utilizado
        /// </summary>
        public string Provider { get; set; } = string.Empty;

        /// <summary>
        /// Resultado de la validación
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Puntuación de confianza (0-100)
        /// </summary>
        public int ConfidenceScore { get; set; }

        /// <summary>
        /// Información extraída del documento
        /// </summary>
        public string ExtractedData { get; set; } = string.Empty;

        /// <summary>
        /// Datos proporcionados para comparación
        /// </summary>
        public string ProvidedData { get; set; } = string.Empty;

        /// <summary>
        /// Detalles de la validación por campo
        /// </summary>
        public List<FieldValidationResult> FieldValidations { get; set; } = new();

        /// <summary>
        /// Mensaje explicativo del resultado
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Respuesta completa de la IA
        /// </summary>
        public string AIResponse { get; set; } = string.Empty;
    }

    /// <summary>
    /// Resultado de validación por campo
    /// </summary>
    public class FieldValidationResult
    {
        /// <summary>
        /// Nombre del campo
        /// </summary>
        public string FieldName { get; set; } = string.Empty;

        /// <summary>
        /// Valor extraído del documento
        /// </summary>
        public string? ExtractedValue { get; set; }

        /// <summary>
        /// Valor proporcionado en JSON
        /// </summary>
        public string? ProvidedValue { get; set; }

        /// <summary>
        /// Si el campo coincide
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// Puntuación de confianza para este campo
        /// </summary>
        public int ConfidenceScore { get; set; }

        /// <summary>
        /// Observaciones sobre la validación del campo
        /// </summary>
        public string? Notes { get; set; }
    }
}
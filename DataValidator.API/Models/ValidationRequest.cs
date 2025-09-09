using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DataValidator.API.Models
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
        /// Tipo de documento para contexto adicional (certificado, factura, contrato, etc.)
        /// </summary>
        public string? DocumentType { get; set; }

        /// <summary>
        /// Campos específicos a validar (opcional)
        /// </summary>
        public List<string>? FieldsToValidate { get; set; }
    }
}

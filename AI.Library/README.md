# AI.Library

**AI.Library** es una librería .NET que proporciona una interfaz unificada y fácil de usar para trabajar con servicios de Inteligencia Artificial, incluyendo extracción de datos de imágenes y PDFs, validación de datos y traducción de texto.

## 🚀 Características Principales

- **Extracción de datos de imágenes**: Utiliza IA para extraer información estructurada de imágenes
- **Procesamiento de PDFs**: Convierte PDFs a imágenes y extrae datos de cada página
- **Validación de datos**: Compara datos extraídos contra referencias JSON
- **Traducción de texto**: Traduce texto entre diferentes idiomas
- **Múltiples proveedores**: Soporte para OpenAI y Google Gemini
- **Inyección de dependencias**: Integración nativa con el contenedor de DI de .NET
- **Logging integrado**: Registro detallado de operaciones

## 📦 Instalación

```bash
# Instalar el paquete principal
dotnet add package AI.Library

# Dependencias adicionales para procesamiento de PDF
dotnet add package IronPdf
dotnet add package UglyToad.PdfPig
```

## ⚙️ Configuración

### Opción 1: Configuración mediante appsettings.json

```json
{
  "AIModels": {
    "AnalysisModel": {
      "Provider": "OpenAI",
      "Model": "gpt-4o",
      "ApiKey": "tu-api-key-aqui",
      "BaseUrl": "https://api.openai.com/v1/chat/completions",
      "Temperature": 0.1,
      "MaxTokens": 4000
    },
    "VisionModel": {
      "Provider": "OpenAI",
      "Model": "gpt-4o",
      "ApiKey": "tu-api-key-aqui",
      "BaseUrl": "https://api.openai.com/v1/chat/completions",
      "Temperature": 0.1,
      "MaxTokens": 4000
    }
  }
}
```

```csharp
// En Program.cs o Startup.cs
using AI.Library.Extensions;

builder.Services.AddAILibrary(builder.Configuration);
```

### Opción 2: Configuración directa con OpenAI

```csharp
using AI.Library.Extensions;

builder.Services.AddAILibraryWithOpenAI(
    openAiApiKey: "tu-api-key-aqui",
    model: "gpt-4o" // opcional, por defecto es gpt-4o
);
```

### Opción 3: Configuración directa con Gemini

```csharp
using AI.Library.Extensions;

builder.Services.AddAILibraryWithGemini(
    geminiApiKey: "tu-api-key-aqui",
    model: "gemini-1.5-flash" // opcional, por defecto es gemini-1.5-flash
);
```

## 🔧 Uso Básico

### Inyección del Servicio

```csharp
using AI.Library.Core;

public class MiControlador : ControllerBase
{
    private readonly IAIService _aiService;

    public MiControlador(IAIService aiService)
    {
        _aiService = aiService;
    }
}
```

### Extracción de Datos de Imágenes

```csharp
public async Task<IActionResult> ExtraerDatosDeImagen(IFormFile imagen)
{
    // Leer la imagen
    using var memoryStream = new MemoryStream();
    await imagen.CopyToAsync(memoryStream);
    var imageData = memoryStream.ToArray();

    // Definir el prompt para la extracción
    var prompt = @"
        Extrae la siguiente información de esta factura:
        - Número de factura
        - Fecha
        - Total
        - Nombre del proveedor
        
        Devuelve el resultado en formato JSON.
    ";

    // Extraer datos
    var resultado = await _aiService.ExtractDataFromImageAsync(
        imageData, 
        imagen.ContentType, 
        prompt
    );

    if (resultado.Success)
    {
        return Ok(new { datos = resultado.ExtractedData });
    }
    
    return BadRequest(resultado.ErrorMessage);
}
```

### Extracción de Datos de PDFs

```csharp
public async Task<IActionResult> ExtraerDatosDePdf(IFormFile pdf)
{
    using var memoryStream = new MemoryStream();
    await pdf.CopyToAsync(memoryStream);
    var pdfData = memoryStream.ToArray();

    var prompt = "Extrae todos los nombres y números de teléfono de este documento";

    var resultado = await _aiService.ExtractDataFromPdfAsync(pdfData, prompt);

    if (resultado.Success)
    {
        return Ok(new { datos = resultado.ExtractedData });
    }
    
    return BadRequest(resultado.ErrorMessage);
}
```

### Validación de Datos

```csharp
public async Task<IActionResult> ValidarDatos()
{
    var datosExtraidos = @"{
        ""numeroFactura"": ""F-2024-001"",
        ""fecha"": ""2024-01-15"",
        ""total"": 1250.00
    }";

    var referenciaJson = @"{
        ""numeroFactura"": ""F-2024-001"",
        ""fecha"": ""2024-01-15"",
        ""total"": 1250.00,
        ""proveedor"": ""Empresa ABC""
    }";

    var reglasValidacion = new[]
    {
        "El número de factura debe coincidir exactamente",
        "La fecha debe estar en formato YYYY-MM-DD",
        "El total debe ser un número positivo"
    };

    var resultado = await _aiService.ValidateDataAsync(
        datosExtraidos, 
        referenciaJson, 
        reglasValidacion
    );

    return Ok(new 
    {
        esValido = resultado.IsValid,
        discrepancias = resultado.Discrepancies,
        confianza = resultado.ConfidenceScore
    });
}
```

### Traducción de Texto

```csharp
public async Task<IActionResult> TraducirTexto(string texto, string idiomaDestino)
{
    var resultado = await _aiService.TranslateTextAsync(
        texto, 
        idiomaDestino, 
        sourceLanguage: "español" // opcional
    );

    if (resultado.Success)
    {
        return Ok(new 
        {
            textoTraducido = resultado.TranslatedText,
            idiomaDetectado = resultado.DetectedSourceLanguage,
            confianza = resultado.ConfidenceScore
        });
    }
    
    return BadRequest(resultado.ErrorMessage);
}
```

## 🏗️ Arquitectura

La librería está organizada siguiendo principios de arquitectura limpia:

```
AI.Library/
├── Core/                    # Servicios principales y contratos
│   ├── IAIService.cs       # Interfaz principal
│   └── AIService.cs        # Implementación principal
├── Models/                  # Modelos de datos
│   ├── AIModelConfiguration.cs
│   ├── ValidationAnalysisResult.cs
│   ├── VisionExtractionResult.cs
│   └── TranslationResult.cs
├── Ports/                   # Interfaces para adaptadores
│   ├── IAiAnalysisProvider.cs
│   ├── IAiVisionProvider.cs
│   └── IPdfProcessor.cs
├── Providers/               # Implementaciones de proveedores
│   └── AI/
│       ├── OpenAiAnalysisAdapter.cs
│       └── GeminiAnalysisAdapter.cs
├── Processors/              # Procesadores especializados
│   └── PdfProcessorAdapter.cs
└── Extensions/              # Extensiones para DI
    └── ServiceCollectionExtensions.cs
```

## 🔌 Extensibilidad

### Agregar un Nuevo Proveedor de IA

1. Implementa las interfaces `IAiAnalysisProvider` y/o `IAiVisionProvider`:

```csharp
public class MiProveedorCustom : IAiAnalysisProvider, IAiVisionProvider
{
    public string ProviderName => "MiProveedor";

    public async Task<ValidationAnalysisResult> AnalyzeDataAsync(string prompt)
    {
        // Tu implementación aquí
    }

    public async Task<VisionExtractionResult> ExtractDataFromImageAsync(
        byte[] imageData, string mimeType, string prompt)
    {
        // Tu implementación aquí
    }
}
```

2. Regístralo en el contenedor de DI:

```csharp
services.AddTransient<MiProveedorCustom>();
services.AddTransient<IAiAnalysisProvider, MiProveedorCustom>();
```

## 📝 Logging

La librería utiliza `ILogger<T>` para el registro de eventos. Configura el logging en tu aplicación:

```csharp
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);
```

## 🚨 Manejo de Errores

Todos los métodos devuelven objetos de resultado que incluyen:
- `Success`: Indica si la operación fue exitosa
- `ErrorMessage`: Mensaje de error en caso de fallo
- Datos específicos del resultado

```csharp
var resultado = await _aiService.ExtractDataFromImageAsync(imageData, mimeType, prompt);

if (!resultado.Success)
{
    _logger.LogError("Error en extracción: {Error}", resultado.ErrorMessage);
    // Manejar el error apropiadamente
}
```

## 🔒 Seguridad

- **Nunca hardcodees las API keys** en el código
- Usa variables de entorno o Azure Key Vault para las claves
- Implementa rate limiting en tus endpoints
- Valida y sanitiza todas las entradas

## 📊 Rendimiento

- Los servicios están registrados como `Transient` para evitar problemas de concurrencia
- Usa `HttpClient` reutilizable para las llamadas a APIs
- Considera implementar caché para resultados frecuentes
- Monitorea el uso de tokens de las APIs

## 🤝 Contribución

Para contribuir al proyecto:

1. Fork el repositorio
2. Crea una rama para tu feature (`git checkout -b feature/nueva-funcionalidad`)
3. Commit tus cambios (`git commit -am 'Agregar nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Crea un Pull Request

## 📄 Licencia

Este proyecto está bajo la licencia MIT. Ver el archivo `LICENSE` para más detalles.

## 🆘 Soporte

Para reportar bugs o solicitar nuevas funcionalidades, por favor crea un issue en el repositorio de GitHub.

---

**AI.Library** - Simplificando el uso de IA en aplicaciones .NET 🚀
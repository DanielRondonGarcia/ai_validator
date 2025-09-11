# AI.Library - Ejemplos de Uso

Esta carpeta contiene ejemplos prácticos de cómo usar AI.Library en diferentes escenarios.

## 📁 Estructura de Ejemplos

### `BasicUsageExample.cs`
Ejemplos básicos que muestran las funcionalidades principales:
- ✅ Extracción de datos de imágenes
- ✅ Validación de datos JSON
- ✅ Traducción de texto
- ✅ Procesamiento de archivos PDF
- ✅ Configuración con Dependency Injection

### `ConsoleApp/`
Aplicación de consola completa e interactiva que demuestra:
- ✅ Configuración desde `appsettings.json`
- ✅ Menú interactivo para probar todas las funcionalidades
- ✅ Manejo de errores y logging
- ✅ Ejemplos con archivos reales

### `appsettings.example.json`
Archivo de configuración de ejemplo que muestra:
- ✅ Configuración de proveedores de IA (OpenAI, Gemini)
- ✅ Configuración de logging
- ✅ Configuración de HttpClient

## 🚀 Cómo Ejecutar los Ejemplos

### Prerequisitos
1. .NET 8.0 o superior
2. API Key de OpenAI o Google Gemini
3. Visual Studio 2022 o VS Code

### Configuración Rápida

1. **Clonar y configurar:**
   ```bash
   git clone <tu-repositorio>
   cd AI.Library/Examples
   ```

2. **Configurar API Keys:**
   - Copia `appsettings.example.json` a `ConsoleApp/appsettings.json`
   - Agrega tus API keys en el archivo de configuración
   - O configura variables de entorno:
     ```bash
     set AIModelsConfiguration__OpenAI__ApiKey=tu-api-key
     set AIModelsConfiguration__Gemini__ApiKey=tu-api-key
     ```

3. **Ejecutar aplicación de consola:**
   ```bash
   cd ConsoleApp
   dotnet run
   ```

### Ejemplo Básico en Código

```csharp
using AI.Library.Core;
using AI.Library.Extensions;
using Microsoft.Extensions.DependencyInjection;

// Configurar servicios
var services = new ServiceCollection();
services.AddLogging();
services.AddAILibraryWithOpenAI("tu-api-key", "gpt-4o");

var serviceProvider = services.BuildServiceProvider();
var aiService = serviceProvider.GetRequiredService<IAIService>();

// Traducir texto
var resultado = await aiService.TranslateTextAsync(
    "Hola mundo", 
    "inglés"
);

if (resultado.Success)
{
    Console.WriteLine($"Traducción: {resultado.TranslatedText}");
}
```

## 📋 Ejemplos Disponibles

### 1. Extracción de Datos de Imagen
```csharp
var imageData = await File.ReadAllBytesAsync("factura.jpg");
var prompt = "Extrae el número de factura, fecha y total";

var resultado = await aiService.ExtractDataFromImageAsync(
    imageData, 
    "image/jpeg", 
    prompt
);
```

### 2. Procesamiento de PDF
```csharp
var pdfData = await File.ReadAllBytesAsync("documento.pdf");
var prompt = "Extrae todos los nombres y fechas";

var resultado = await aiService.ExtractDataFromPdfAsync(pdfData, prompt);
```

### 3. Validación de Datos
```csharp
var datosExtraidos = @"{
    ""nombre"": ""Juan"",
    ""edad"": 30
}";

var datosReferencia = @"{
    ""nombre"": ""Juan Pérez"",
    ""edad"": 30
}";

var reglas = new[] { "El nombre debe coincidir", "La edad debe ser válida" };

var resultado = await aiService.ValidateDataAsync(
    datosExtraidos, 
    datosReferencia, 
    reglas
);
```

### 4. Traducción de Texto
```csharp
var resultado = await aiService.TranslateTextAsync(
    "Hello, how are you?", 
    "español"
);

Console.WriteLine($"Traducción: {resultado.TranslatedText}");
Console.WriteLine($"Confianza: {resultado.ConfidenceScore:P}");
```

## ⚙️ Configuración Avanzada

### Usando Múltiples Proveedores
```csharp
services.AddAILibrary(configuration);

// En appsettings.json:
{
  "AIModelsConfiguration": {
    "OpenAI": { "ApiKey": "...", "Model": "gpt-4o" },
    "Gemini": { "ApiKey": "...", "Model": "gemini-1.5-pro" },
    "DefaultProvider": "OpenAI"
  }
}
```

### Configuración de Logging
```csharp
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddFilter("AI.Library", LogLevel.Information);
});
```

## 🔧 Personalización

### Crear Adaptador Personalizado
```csharp
public class MiAdaptadorPersonalizado : IAiAnalysisProvider
{
    public async Task<ValidationAnalysisResult> AnalyzeDataAsync(
        string extractedData, 
        string referenceData, 
        string[] validationRules)
    {
        // Tu lógica personalizada
        return new ValidationAnalysisResult
        {
            Success = true,
            IsValid = true,
            // ...
        };
    }
}

// Registrar en DI
services.AddScoped<IAiAnalysisProvider, MiAdaptadorPersonalizado>();
```

## 📊 Métricas y Monitoreo

Todos los ejemplos incluyen logging detallado:
- ✅ Tiempo de respuesta de APIs
- ✅ Tokens utilizados
- ✅ Errores y reintentos
- ✅ Metadatos de procesamiento

## 🛠️ Solución de Problemas

### Error: "API Key no configurada"
- Verifica que el API key esté en `appsettings.json` o variables de entorno
- Asegúrate de que el formato sea correcto

### Error: "Timeout en la solicitud"
- Aumenta `TimeoutSeconds` en la configuración
- Verifica tu conexión a internet

### Error: "Archivo no encontrado"
- Verifica que las rutas de archivos sean absolutas
- Asegúrate de que los archivos existan

## 📚 Recursos Adicionales

- [Documentación Principal](../README.md)
- [Guía de Configuración](../README.md#configuración)
- [API Reference](../README.md#api-reference)

## 🤝 Contribuir

¿Tienes un ejemplo útil? ¡Contribuye!

1. Fork el repositorio
2. Crea una rama para tu ejemplo
3. Agrega documentación clara
4. Envía un Pull Request

---

**💡 Tip:** Comienza con la aplicación de consola para una experiencia interactiva completa.
# API de Validación de Documentos con IA

Esta es una API RESTful construida con .NET 8 y C# que utiliza inteligencia artificial para analizar y validar el contenido de documentos. El proyecto está estructurado siguiendo los principios de la **Arquitectura Hexagonal (Puertos y Adaptadores)**, lo que permite una gran flexibilidad para cambiar o añadir nuevas tecnologías (como diferentes proveedores de IA) con un impacto mínimo en la lógica de negocio principal.

## Arquitectura Hexagonal (Puertos y Adaptadores)

### ¿Qué es la Arquitectura Hexagonal?

La **Arquitectura Hexagonal**, también conocida como **Puertos y Adaptadores**, es un patrón arquitectónico que busca aislar la lógica de negocio de las dependencias externas (bases de datos, APIs, frameworks, etc.). El "hexágono" representa el núcleo de la aplicación, mientras que los "puertos" son interfaces que definen cómo el núcleo se comunica con el exterior, y los "adaptadores" son las implementaciones concretas de esas interfaces.

### Estructura del Proyecto

El proyecto está dividido en tres capas principales, cada una con una responsabilidad clara:

#### 🏛️ `DataValidator.Domain` - El Núcleo (Hexágono)
Este es el **corazón** de la aplicación y contiene:
- **Lógica de Negocio**: Las reglas y procesos fundamentales de validación de documentos
- **Modelos de Dominio**: Entidades como `ValidationResult`, `VisionExtractionResult`, `ValidationAnalysisResult`
- **Puertos (Interfaces)**: Contratos que definen cómo el núcleo se comunica con el exterior:
  - `IAiVisionProvider`: Para extraer datos de imágenes/PDFs
  - `IAiAnalysisProvider`: Para analizar y validar datos extraídos
  - `IPdfProcessor`: Para procesar documentos PDF
  - `IVisionExtractionService`: Para orquestar la extracción de datos
  - `IAnalysisValidationService`: Para orquestar la validación

**Característica clave**: Esta capa **NO depende** de ninguna otra capa, tecnología específica o framework externo.

#### 🔌 `DataValidator.Infrastructure` - Los Adaptadores
Contiene las **implementaciones concretas** de los puertos definidos en el dominio:
- **Adaptadores de IA**:
  - `OpenAiVisionAdapter`: Implementa `IAiVisionProvider` para OpenAI GPT-4 Vision
  - `GeminiVisionAdapter`: Implementa `IAiVisionProvider` para Google Gemini Vision
  - `OpenAiAnalysisAdapter`: Implementa `IAiAnalysisProvider` para OpenAI GPT-4
  - `GeminiAnalysisAdapter`: Implementa `IAiAnalysisProvider` para Google Gemini
- **Procesadores**:
  - `PdfProcessorAdapter`: Implementa `IPdfProcessor` usando bibliotecas específicas

#### 🌐 `DataValidator.API` - El Adaptador Primario
Es la **puerta de entrada** a la aplicación:
- **Controladores**: Exponen endpoints HTTP (`ValidationController`)
- **Servicios de Aplicación**: Orquestan la lógica del dominio (`VisionExtractionService`, `AnalysisValidationService`)
- **Configuración**: Inyección de dependencias y configuración de adaptadores
- **Modelos de API**: DTOs para comunicación HTTP (`ValidationRequest`)

### 🎯 Ventajas de la Arquitectura Hexagonal

#### 1. **Independencia Tecnológica**
- Puedes cambiar de OpenAI a Google Gemini sin tocar la lógica de negocio
- Fácil migración entre diferentes frameworks o bibliotecas
- El núcleo permanece estable ante cambios tecnológicos

#### 2. **Testabilidad Superior**
- Puedes crear mocks de los adaptadores para pruebas unitarias
- La lógica de negocio se puede probar de forma aislada
- Tests más rápidos y confiables

#### 3. **Flexibilidad y Extensibilidad**
- Agregar nuevos proveedores de IA es trivial (solo crear un nuevo adaptador)
- Soporte para múltiples proveedores simultáneamente
- Fácil implementación de patrones como Circuit Breaker o Retry

#### 4. **Mantenibilidad**
- Separación clara de responsabilidades
- Cambios en adaptadores no afectan el núcleo
- Código más limpio y organizado

#### 5. **Escalabilidad**
- Diferentes equipos pueden trabajar en diferentes capas
- Despliegue independiente de componentes
- Fácil implementación de microservicios en el futuro

## Cómo Usar la API

### Endpoint Disponible

Con la nueva arquitectura, solo hay un endpoint principal que maneja todas las validaciones:

-   **`POST /api/validation/cross-validate`**: Valida un documento (PDF o imagen) utilizando los proveedores de IA configurados.

### Petición

La petición debe ser de tipo `multipart/form-data` y contener los siguientes campos:

1.  `File`: El archivo PDF o imagen que se desea validar.
2.  `JsonData`: Una cadena de texto que contiene los datos en formato JSON con los que se comparará el contenido del archivo.
3.  `DocumentType`: (Opcional) Una cadena que describe el tipo de documento para dar más contexto a la IA (ej: "factura", "contrato").
4.  `FieldsToValidate`: (Opcional) Una lista de campos específicos que se deben validar.

### Ejemplo de Uso

```bash
curl -X POST "http://localhost:5252/api/validation/cross-validate" \
-H "Content-Type: multipart/form-data" \
-F "File=@/ruta/a/tu/documento.pdf" \
-F "JsonData={\\"nombre\\":\\"John Doe\\"}" \
-F "DocumentType=CV"
```

## 🔧 Configuración

La configuración de la aplicación se encuentra en el archivo `appsettings.json` (o `appsettings.Development.json` para desarrollo). Aquí puedes configurar los modelos de IA que deseas utilizar:

```json
{
  "AIModels": {
    "VisionModel": {
      "Provider": "OpenAI",
      "Model": "gpt-4o",
      "ApiKey": "tu-api-key-de-openai",
      "BaseUrl": "https://api.openai.com/v1"
    },
    "AnalysisModel": {
      "Provider": "OpenAI",
      "Model": "gpt-4o",
      "ApiKey": "tu-api-key-de-openai",
      "BaseUrl": "https://api.openai.com/v1"
    }
  }
}
```

## 🔄 Cómo Cambiar el Adaptador de Visión IA

Gracias a la arquitectura hexagonal, cambiar entre diferentes proveedores de IA es extremadamente sencillo y **no requiere cambios en el código**. Solo necesitas modificar la configuración.

### Ejemplo 1: Cambiar de OpenAI a Google Gemini

**Paso 1**: Modifica el archivo `appsettings.Development.json`:

```json
{
  "AIModels": {
    "VisionModel": {
      "Provider": "Google",           // ← Cambiar de "OpenAI" a "Google"
      "Model": "gemini-1.5-flash",    // ← Modelo específico de Gemini
      "ApiKey": "tu-api-key-de-google", // ← Tu API key de Google
      "BaseUrl": "https://generativelanguage.googleapis.com/v1beta" // ← URL de Gemini
    },
    "AnalysisModel": {
      "Provider": "Google",
      "Model": "gemini-1.5-flash",
      "ApiKey": "tu-api-key-de-google",
      "BaseUrl": "https://generativelanguage.googleapis.com/v1beta"
    }
  }
}
```

**Paso 2**: Reinicia la aplicación. ¡Eso es todo!

### Ejemplo 2: Usar Diferentes Proveedores para Visión y Análisis

Puedes usar **OpenAI para visión** y **Gemini para análisis** (o viceversa):

```json
{
  "AIModels": {
    "VisionModel": {
      "Provider": "OpenAI",              // ← OpenAI para extraer datos de imágenes
      "Model": "gpt-4o",
      "ApiKey": "tu-api-key-de-openai",
      "BaseUrl": "https://api.openai.com/v1"
    },
    "AnalysisModel": {
      "Provider": "Google",             // ← Gemini para analizar los datos extraídos
      "Model": "gemini-1.5-flash",
      "ApiKey": "tu-api-key-de-google",
      "BaseUrl": "https://generativelanguage.googleapis.com/v1beta"
    }
  }
}
```

### ¿Cómo Funciona Internamente?

Cuando cambias el `Provider` en la configuración:

1. **El sistema de inyección de dependencias** lee la configuración al iniciar
2. **Selecciona automáticamente** el adaptador correcto:
   - Si `Provider = "OpenAI"` → Usa `OpenAiVisionAdapter`
   - Si `Provider = "Google"` → Usa `GeminiVisionAdapter`
3. **La lógica de negocio permanece igual** - solo cambia la implementación del adaptador
4. **Los servicios de aplicación** (`VisionExtractionService`, `AnalysisValidationService`) siguen funcionando sin modificaciones

### Ventajas de Este Enfoque

✅ **Sin cambios de código**: Solo modificas configuración
✅ **Cambio en caliente**: Reinicia la app y ya tienes el nuevo proveedor
✅ **Flexibilidad total**: Combina diferentes proveedores según tus necesidades
✅ **Fácil testing**: Puedes probar diferentes proveedores rápidamente
✅ **Independencia de vendor**: No quedas atado a un solo proveedor de IA

## 🚀 Cómo Agregar un Nuevo Proveedor de IA

Una de las mayores ventajas de la arquitectura hexagonal es la facilidad para agregar nuevos proveedores. Aquí te mostramos cómo agregar soporte para un nuevo proveedor de IA (por ejemplo, **Claude de Anthropic**):

### Paso 1: Crear el Nuevo Adaptador

Crea una nueva clase en `DataValidator.Infrastructure/Providers/AI/`:

```csharp
// ClaudeVisionAdapter.cs
public class ClaudeVisionAdapter : IAiVisionProvider
{
    private readonly AIModelsConfiguration _aiConfig;
    private readonly HttpClient _httpClient;

    public ClaudeVisionAdapter(AIModelsConfiguration aiConfig, HttpClient httpClient)
    {
        _aiConfig = aiConfig;
        _httpClient = httpClient;
    }

    public async Task<VisionExtractionResult> ExtractDataFromImageAsync(
        byte[] imageData, 
        string prompt, 
        CancellationToken cancellationToken = default)
    {
        // Implementar la lógica específica para Claude API
        var model = _aiConfig.VisionModel.Model;
        var apiKey = _aiConfig.VisionModel.ApiKey;
        var baseUrl = _aiConfig.VisionModel.BaseUrl;
        
        // ... código específico para Claude ...
        
        return new VisionExtractionResult
        {
            ExtractedData = extractedText,
            IsSuccessful = true,
            Provider = "Claude"
        };
    }
}
```

### Paso 2: Registrar el Adaptador

Modifica `Program.cs` en `DataValidator.API` para registrar el nuevo adaptador:

```csharp
// En Program.cs
builder.Services.AddScoped<IAiVisionProvider>(provider =>
{
    var config = provider.GetRequiredService<AIModelsConfiguration>();
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient();

    return config.VisionModel.Provider.ToLower() switch
    {
        "openai" => new OpenAiVisionAdapter(config, httpClient),
        "google" => new GeminiVisionAdapter(config, httpClient),
        "claude" => new ClaudeVisionAdapter(config, httpClient), // ← Nuevo adaptador
        _ => throw new NotSupportedException($"Provider {config.VisionModel.Provider} not supported")
    };
});
```

### Paso 3: Configurar el Nuevo Proveedor

Agrega la configuración en `appsettings.json`:

```json
{
  "AIModels": {
    "VisionModel": {
      "Provider": "Claude",
      "Model": "claude-3-vision",
      "ApiKey": "tu-api-key-de-claude",
      "BaseUrl": "https://api.anthropic.com/v1"
    }
  }
}
```

### Paso 4: ¡Listo para Usar!

Ahora puedes usar Claude simplemente cambiando la configuración. **No necesitas modificar ningún otro código**.

### 🎯 Beneficios de Este Enfoque

- **Aislamiento**: Cada adaptador es independiente
- **Reutilización**: La lógica de negocio se reutiliza para todos los proveedores
- **Testing**: Puedes probar cada adaptador por separado
- **Mantenimiento**: Cambios en un proveedor no afectan a otros
- **Escalabilidad**: Agregar proveedores es lineal, no exponencial en complejidad

## 🏗️ Patrones Avanzados y Mejores Prácticas

### 1. **Patrón Factory para Adaptadores**

El proyecto utiliza el patrón Factory para crear adaptadores dinámicamente:

```csharp
// En Program.cs - Factory Pattern
builder.Services.AddScoped<IAiVisionProvider>(provider =>
{
    var config = provider.GetRequiredService<AIModelsConfiguration>();
    return AiProviderFactory.CreateVisionProvider(config, provider);
});
```

### 2. **Inyección de Dependencias Limpia**

Cada adaptador recibe solo las dependencias que necesita:

```csharp
public class OpenAiVisionAdapter : IAiVisionProvider
{
    // Solo las dependencias necesarias
    private readonly AIModelsConfiguration _aiConfig;
    private readonly HttpClient _httpClient;
    
    // No depende de frameworks específicos
    // No conoce detalles de otros adaptadores
}
```

### 3. **Manejo de Errores Consistente**

Todos los adaptadores devuelven el mismo tipo de resultado:

```csharp
public class VisionExtractionResult
{
    public string ExtractedData { get; set; }
    public bool IsSuccessful { get; set; }
    public string ErrorMessage { get; set; }
    public string Provider { get; set; }
}
```

### 4. **Testing con Mocks**

La arquitectura facilita las pruebas unitarias:

```csharp
[Test]
public async Task ExtractData_ShouldReturnValidResult()
{
    // Arrange
    var mockVisionProvider = new Mock<IAiVisionProvider>();
    var service = new VisionExtractionService(mockVisionProvider.Object);
    
    // Act & Assert - Sin dependencias externas
}
```

### 🎯 Principios SOLID Aplicados

- **S** - Single Responsibility: Cada adaptador tiene una sola responsabilidad
- **O** - Open/Closed: Abierto para extensión (nuevos adaptadores), cerrado para modificación
- **L** - Liskov Substitution: Cualquier adaptador puede reemplazar a otro
- **I** - Interface Segregation: Interfaces específicas y pequeñas
- **D** - Dependency Inversion: Dependemos de abstracciones, no de concreciones

### 📋 Configuración Completa de Ejemplo

```json
{
  "AIModels": {
    "VisionModel": {
      "Provider": "OpenAI", // Opciones: "OpenAI", "Google"
      "Model": "gpt-4-vision-preview",
      "ApiKey": "TU_CLAVE_DE_API_DE_OPENAI",
      "BaseUrl": "https://api.openai.com/v1"
    },
    "AnalysisModel": {
      "Provider": "Google", // Opciones: "OpenAI", "Google"
      "Model": "gemini-pro",
      "ApiKey": "TU_CLAVE_DE_API_DE_GOOGLE",
      "BaseUrl": "https://generativelanguage.googleapis.com/v1beta"
    },
    "AlternativeVisionModel": {
      "Provider": "Google",
      "Model": "gemini-pro-vision"
    },
    "AlternativeAnalysisModel": {
      "Provider": "OpenAI",
      "Model": "gpt-4-turbo-preview"
    }
  }
}
```

### Ejemplo: Cómo cambiar el proveedor de Visión Artificial

Para cambiar el proveedor de extracción de datos de imágenes de `OpenAI` a `Google`, simplemente modifica la sección `VisionModel` en tu `appsettings.Development.json` (o el archivo de configuración que corresponda):

**Antes (usando OpenAI):**
```json
"VisionModel": {
  "Provider": "OpenAI",
  "Model": "gpt-4-vision-preview",
  "ApiKey": "TU_CLAVE_DE_API_DE_OPENAI",
  "BaseUrl": "https://api.openai.com/v1"
}
```

**Después (usando Google Gemini):**
```json
"VisionModel": {
  "Provider": "Google",
  "Model": "gemini-pro-vision",
  "ApiKey": "TU_CLAVE_DE_API_DE_GOOGLE",
  "BaseUrl": "https://generativelanguage.googleapis.com/v1beta"
}
```

La aplicación detectará automáticamente el cambio en la configuración y utilizará el `GeminiVisionAdapter` en lugar del `OpenAiVisionAdapter` la próxima vez que se inicie, sin necesidad de recompilar o cambiar una sola línea de código en la lógica de negocio.

## 📦 Cómo Compilar y Ejecutar el Proyecto

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Una clave API válida de OpenAI o Google Gemini

### Pasos

1.  **Clonar el repositorio y navegar a la raíz del proyecto**
    ```bash
    git clone <repository-url>
    cd ai_validator
    ```

2.  **Configurar las Claves de API:**
    Abre el archivo `DataValidator.API/appsettings.Development.json` y añade tus claves de API para los proveedores que desees utilizar:
    ```json
    {
      "AIModels": {
        "VisionModel": {
          "Provider": "OpenAI",
          "ApiKey": "tu-api-key-real-aqui"
        }
      }
    }
    ```

3.  **Restaurar dependencias y compilar:**
    Desde la raíz del repositorio, donde se encuentra el archivo `DataValidator.sln`, ejecuta:
    ```bash
    dotnet restore
    dotnet build
    ```

4.  **Ejecutar la API:**
    ```bash
    dotnet run --project DataValidator.API
    ```
    La API se ejecutará y estará disponible en las URLs que se muestran en la consola (por ejemplo, `http://localhost:5252`).

5.  **Acceder a la Documentación de Swagger:**
    Una vez que la API esté en ejecución, puedes acceder a la documentación interactiva de Swagger en la URL `http://localhost:5252/swagger`.

## 🎯 Resumen: ¿Por Qué Arquitectura Hexagonal?

Este proyecto demuestra las **ventajas reales** de la arquitectura hexagonal:

### ✅ **Flexibilidad Demostrada**
- Cambias de OpenAI a Gemini en **segundos** (solo configuración)
- Agregas nuevos proveedores sin tocar código existente
- Combinas diferentes proveedores según necesidades

### ✅ **Mantenibilidad Comprobada**
- Lógica de negocio separada de detalles técnicos
- Cada adaptador es independiente y testeable
- Cambios en APIs externas no rompen el núcleo

### ✅ **Escalabilidad Preparada**
- Fácil migración a microservicios
- Soporte para múltiples proveedores simultáneos
- Arquitectura preparada para crecimiento

### 🚀 **Próximos Pasos Sugeridos**

1. **Implementar Circuit Breaker**: Para manejar fallos de APIs externas
2. **Agregar Cache**: Para optimizar llamadas repetitivas
3. **Implementar Retry Policy**: Para mejorar resilencia
4. **Agregar Métricas**: Para monitoreo y observabilidad
5. **Soporte Multi-tenant**: Para diferentes configuraciones por cliente

**La arquitectura hexagonal no es solo teoría - es una herramienta práctica que hace tu código más flexible, testeable y mantenible.** 🎯

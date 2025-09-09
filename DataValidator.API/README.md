# API de Validación de Documentos con IA

Esta es una API RESTful construida con .NET 8 y C# que utiliza inteligencia artificial para analizar y validar el contenido de documentos. El proyecto está estructurado siguiendo los principios de la **Arquitectura Hexagonal (Puertos y Adaptadores)**, lo que permite una gran flexibilidad para cambiar o añadir nuevas tecnologías (como diferentes proveedores de IA) con un impacto mínimo en la lógica de negocio principal.

## Arquitectura Hexagonal

El proyecto está dividido en tres capas principales, cada una con una responsabilidad clara:

-   `DataValidator.Domain`: Este es el núcleo de la aplicación. Contiene la lógica de negocio, los modelos de dominio (por ejemplo, `ValidationResult`) y las interfaces (conocidas como **Puertos**) que definen los contratos para cualquier servicio externo que la aplicación necesite (por ejemplo, `IAiVisionProvider`, `IAiAnalysisProvider`). Esta capa no depende de ninguna otra capa del proyecto, lo que la hace completamente independiente de la tecnología.

-   `DataValidator.Infrastructure`: Esta capa contiene las implementaciones concretas de los puertos definidos en el Dominio. Estas implementaciones se conocen como **Adaptadores**. Por ejemplo, aquí es donde se encuentra el código específico para llamar a la API de OpenAI o a la de Google Gemini. Si quisiéramos añadir soporte para un nuevo proveedor de IA, simplemente añadiríamos un nuevo adaptador en este proyecto.

-   `DataValidator.API`: Esta es la capa de entrada de la aplicación. En este caso, es una API web que expone endpoints HTTP. Actúa como el **adaptador primario** que dirige la aplicación. Es responsable de recibir las solicitudes, llamar a los servicios de la aplicación (que orquestan la lógica del dominio) y devolver las respuestas. También es responsable de la configuración y de la inyección de dependencias, es decir, de "conectar" los adaptadores de la infraestructura a los puertos del dominio.

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

## Configuración y Cambio de Proveedor de IA

La principal ventaja de la arquitectura hexagonal es la facilidad para cambiar las dependencias externas. Para cambiar el proveedor de IA para la visión o el análisis, no necesitas cambiar el código, solo tienes que modificar el archivo `appsettings.json`.

### `appsettings.json`

Este archivo contiene la configuración de los modelos de IA a utilizar.

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

## Cómo Compilar y Ejecutar el Proyecto

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Pasos

1.  **Clonar el repositorio y navegar a la raíz del proyecto.**
2.  **Configurar las Claves de API:**
    Abre el archivo `DataValidator.API/appsettings.Development.json` y añade tus claves de API para los proveedores que desees utilizar.
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

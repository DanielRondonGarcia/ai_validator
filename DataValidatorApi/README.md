# API de Validación de Documentos con IA

Esta es una API RESTful simple construida con .NET 8 y C# que utiliza inteligencia artificial para analizar el contenido de documentos PDF.

## Descripción

El propósito de esta API es permitir a un sistema externo validar el contenido de un documento (por ahora, solo PDF) haciéndole una pregunta en lenguaje natural. Por ejemplo, se puede subir un certificado de un curso y preguntar: "¿Este certificado confirma la finalización del curso 'Introducción a la Programación'?".

La API utiliza:
- **IronPdf** para procesar el archivo PDF y convertir su primera página en una imagen.
- **Google Gemini Pro Vision** (a través del SDK `Google_GenerativeAI`) para analizar la imagen en el contexto de la pregunta proporcionada por el usuario.

## Cómo Usar la API

### Endpoint

- `POST /api/validation/validate`

### Petición

La petición debe ser de tipo `multipart/form-data` y contener dos campos:

1.  `file`: El archivo PDF que se desea validar.
2.  `schema`: Una cadena de texto que contiene la pregunta o prompt en lenguaje natural para la IA.

**Ejemplo de uso con `curl`:**

```bash
curl -X POST "http://localhost:5252/api/validation/validate" \
-H "Content-Type: multipart/form-data" \
-F "file=@/ruta/a/tu/documento.pdf" \
-F "schema=¿Este documento es un certificado de finalización para el curso de .NET?"
```

### Respuesta Exitosa (Código 200)

La API devolverá un objeto JSON con la respuesta generada por el modelo de IA.

```json
{
  "fileName": "documento.pdf",
  "prompt": "¿Este documento es un certificado de finalización para el curso de .NET?",
  "ai_Response": "Sí, este documento es un certificado de finalización para el curso de .NET, otorgado a Jules."
}
```

### Respuestas de Error

- **Código 400 (Bad Request):** Si falta el archivo, el prompt, o la clave de API de Google no está configurada.
- **Código 500 (Internal Server Error):** Si ocurre un error durante el procesamiento del PDF o la comunicación con la API de Google.

## Cómo Compilar y Ejecutar el Proyecto

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Una clave de API de Google para los servicios de IA Generativa. Puedes obtenerla desde [Google AI Studio](https://aistudio.google.com/app/apikey).

### Pasos

1.  **Clonar el repositorio:**
    ```bash
    git clone <URL_DEL_REPOSITORIO>
    cd <NOMBRE_DEL_REPOSITORIO>/DataValidatorApi
    ```

2.  **Configurar la Clave de API de Google:**
    La aplicación necesita acceso a tu clave de API de Google. Debes configurarla como una variable de entorno.

    En Linux/macOS:
    ```bash
    export GOOGLE_API_KEY="TU_CLAVE_DE_API_AQUI"
    ```

    En Windows (PowerShell):
    ```bash
    $env:GOOGLE_API_KEY="TU_CLAVE_DE_API_AQUI"
    ```

3.  **Restaurar dependencias y compilar:**
    Navega al directorio del proyecto `DataValidatorApi` y ejecuta:
    ```bash
    dotnet restore
    dotnet build
    ```

4.  **Ejecutar la API:**
    ```bash
    dotnet run
    ```
    La API se ejecutará y estará disponible en las URLs que se muestran en la consola (por ejemplo, `http://localhost:5252`).

5.  **Acceder a la Documentación de Swagger:**
    Una vez que la API esté en ejecución, puedes acceder a la documentación interactiva de Swagger en la URL `http://localhost:5252/swagger`. Desde allí, puedes probar el endpoint directamente desde tu navegador.

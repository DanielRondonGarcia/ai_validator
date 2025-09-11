using DataValidator.Domain.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataValidator.API.Services
{
    public class PromptBuilderService : IPromptBuilderService
    {
        public string BuildExtractionPrompt(string documentType, List<string>? fieldsToExtract = null)
        {
            var basePrompt = @$"You are an expert document analysis AI specialized in extracting structured data from {documentType} documents.

**TASK**: Extract all relevant information from this document with high accuracy and attention to detail.

**DOCUMENT TYPE**: {documentType}

**EXTRACTION GUIDELINES**:
1. **Accuracy First**: Extract exactly what you see - do not interpret, assume, or fill in missing information
2. **Structured Output**: Organize data logically with clear field names
3. **Data Types**: Preserve original formatting (dates, numbers, text)
4. **Completeness**: Extract all visible text, forms, tables, and structured information
5. **Quality Control**: Double-check critical fields like names, dates, amounts, and IDs

**SPECIAL ATTENTION TO**:
- Names and personal identifiers
- Dates and timestamps
- Monetary amounts and numbers
- Addresses and contact information
- Document numbers and references
- Signatures and stamps
- Tables and structured data

**OUTPUT FORMAT**:
Provide extracted data in clear, structured JSON format:

{{
  ""documentType"": ""{documentType}"",
  ""extractedFields"": {{
    ""fieldName"": ""extracted value"",
    ""anotherField"": ""another value""
  }},
  ""tables"": [
    {{
      ""tableName"": ""description"",
      ""headers"": [""col1"", ""col2""],
      ""rows"": [[""data1"", ""data2""]]
    }}
  ],
  ""metadata"": {{
    ""confidence"": ""high|medium|low"",
    ""documentQuality"": ""clear|moderate|poor"",
    ""extractionNotes"": ""any relevant observations""
  }}
}}";

            if (fieldsToExtract != null && fieldsToExtract.Any())
            {
                var fieldsText = string.Join(", ", fieldsToExtract);
                basePrompt += @$"

**PRIORITY FIELDS** (focus extraction on these specific fields):
{fieldsText}

Ensure these priority fields are extracted with maximum accuracy and detail.";
            }

            return basePrompt;
        }

        public string BuildValidationPrompt(string documentType, List<string> fieldsToValidate, string jsonData)
        {
            var fieldsText = fieldsToValidate != null && fieldsToValidate.Any() 
                ? string.Join(", ", fieldsToValidate)
                : "all available fields";
            
            return @$"Eres un especialista experto en la validación de datos, enfocado en el análisis de {documentType}.

**TAREA:** Realiza una validación de los datos extraídos comparándolos con el `jsonData` proporcionado.

**TIPO DE DOCUMENTO**: {documentType}
**FIELDS TO VALIDATE**: {fieldsText}
**jsonData TO VALIDATE**: {jsonData}

**CRITERIOS DE VALIDACIÓN:**

**1. Enfoque de Validación:**
*   **Crítico:** Céntrate **exclusivamente** en los campos listados en `FIELDS TO VALIDATE`. Ignora cualquier discrepancia en otros campos.
*   Si `FIELDS TO VALIDATE` especifica `all available fields`, entonces y solo entonces, analiza todos los campos del documento.

**2. Verificación de Exactitud:**
*   Compara el valor de `extractedFields` con el valor correspondiente en `jsonData TO VALIDATE`.
*   Identifica discrepancias en el contenido (por ejemplo, nombres o cursos diferentes).

**3. Tratamiento de Formatos:**
*   **Fechas:** No consideres una diferencia de formato como una discrepancia. Si una fecha en texto (ej. `Feb 11, 2023`) y una fecha numérica (ej. `02-11-2023`) representan el mismo día, mes y año, deben ser consideradas como una coincidencia.
*   **Otros Formatos:** Verifica la consistencia en capitalización y ortografía solo si afecta la exactitud del dato.

**FORMATO DE RESPUESTA:**
Proporciona tu análisis en el siguiente formato JSON:

{{
  ""validationSummary"": {{
    ""overallAccuracy"": ""percentage or score"",
    ""criticalIssues"": ""number"",
    ""minorIssues"": ""number"",
    ""validatedFields"": ""number""
  }},
  ""fieldValidation"": [
    {{
      ""fieldName"": ""name of field"",
      ""status"": ""valid|invalid|warning|missing"",
      ""extractedValue"": ""what was extracted"",
      ""expectedValue"": ""what should be (if different)"",
      ""issue"": ""description of problem if any"",
      ""severity"": ""critical|moderate|minor"",
      ""confidence"": ""high|medium|low""
    }}
  ],
  ""recommendations"": [
    ""specific suggestions for improvement""
  ],
  ""qualityScore"": ""overall quality rating (0.0 to 1.0)""
}}

Sé exhaustivo, preciso y proporciona retroalimentación detallada para mejorar la precisión de la validación de datos.
";
        }

        public string BuildDiscrepancyAnalysisPrompt(string documentType, List<string> discrepancies)
        {
            var discrepanciesText = string.Join("\n- ", discrepancies);
            
            return @$"You are an expert data analyst specializing in discrepancy resolution and quality assurance.

**TASK**: Analyze the following discrepancies found during data validation and provide actionable insights.

**IDENTIFIED DISCREPANCIES**:
- {discrepanciesText}

**ANALYSIS FRAMEWORK**:

1. **Root Cause Analysis**:
   - Is this a data entry error?
   - Document quality issue (unclear text, poor scan)?
   - Format interpretation difference?
   - Legitimate data variation?

2. **Impact Assessment**:
   - How critical is this discrepancy?
   - Does it affect document validity?
   - What are the potential consequences?

3. **Resolution Recommendations**:
   - Can this be automatically corrected?
   - Does it require manual review?
   - Should the source document be re-examined?
   - Are there patterns suggesting systematic issues?

4. **Confidence Level**:
   - How certain are you about this discrepancy?
   - Are there alternative interpretations?

**RESPONSE FORMAT**:
Provide a detailed JSON analysis with actionable insights:

{{
  ""overallAssessment"": ""summary of discrepancy analysis"",
  ""discrepancyAnalysis"": [
    {{
      ""discrepancy"": ""original discrepancy description"",
      ""rootCause"": ""likely cause of the discrepancy"",
      ""impactLevel"": ""critical|moderate|minor"",
      ""confidence"": ""number (0.0 to 1.0)"",
      ""recommendation"": ""specific action to resolve"",
      ""alternativeInterpretation"": ""other possible explanations""
    }}
  ],
  ""systematicIssues"": ""patterns or recurring problems identified"",
  ""qualityScore"": ""number (0.0 to 1.0)"",
  ""nextSteps"": ""recommended follow-up actions""
}}

Provide thorough, actionable analysis that helps improve data validation accuracy.";
        }
    }
}
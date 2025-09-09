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

        public string BuildValidationPrompt(string documentType, List<string> fieldsToValidate)
        {
            var fieldsText = fieldsToValidate != null && fieldsToValidate.Any() 
                ? string.Join(", ", fieldsToValidate)
                : "all available fields";
            
            return @$"You are an expert data validation specialist focused on {documentType} document analysis.

**TASK**: Perform comprehensive validation of extracted data against the original document.

**DOCUMENT TYPE**: {documentType}
**FIELDS TO VALIDATE**: {fieldsText}

**VALIDATION CRITERIA**:

1. **Accuracy Verification**:
   - Compare extracted data with source document
   - Identify any transcription errors
   - Check for missing or incomplete information
   - Verify data type consistency

2. **Format Compliance**:
   - Validate date formats and ranges
   - Check numeric values and calculations
   - Verify address and contact information formats
   - Ensure proper capitalization and spelling

3. **Completeness Assessment**:
   - Identify missing required fields
   - Check for partial extractions
   - Verify all visible data was captured

4. **Quality Control**:
   - Flag suspicious or unusual values
   - Identify potential OCR errors
   - Check for logical inconsistencies
   - Validate cross-field relationships

**VALIDATION FOCUS AREAS**:
- Critical identifiers (IDs, names, numbers)
- Financial data (amounts, calculations)
- Dates and temporal information
- Contact and address details
- Document-specific requirements

**OUTPUT FORMAT**:
Provide detailed validation results in JSON format:

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

Be thorough, precise, and provide actionable feedback for improving data extraction accuracy.";
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
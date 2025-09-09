using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

/// <summary>
/// Operation filter to handle file uploads and form data in Swagger documentation
/// </summary>
public class FileUploadOperationFilter : IOperationFilter
{
    /// <summary>
    /// Apply the operation filter to handle multipart/form-data requests
    /// </summary>
    /// <param name="operation">The OpenAPI operation</param>
    /// <param name="context">The operation filter context</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var parameters = context.MethodInfo.GetParameters();
        
        // Check if any parameter has FromForm attribute or is IFormFile
        var hasFormParameters = parameters.Any(p => 
            p.GetCustomAttribute<FromFormAttribute>() != null ||
            p.ParameterType == typeof(IFormFile) ||
            p.ParameterType == typeof(IFormFile[]) ||
            p.ParameterType == typeof(IEnumerable<IFormFile>) ||
            HasFormFileProperty(p.ParameterType));

        if (!hasFormParameters) return;

        // Create multipart/form-data request body
        operation.RequestBody = new OpenApiRequestBody
        {
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, OpenApiSchema>(),
                        Required = new HashSet<string>()
                    }
                }
            }
        };

        var schema = operation.RequestBody.Content["multipart/form-data"].Schema;

        // Process each parameter
        foreach (var parameter in parameters)
        {
            var fromFormAttribute = parameter.GetCustomAttribute<FromFormAttribute>();

            var isComplex = IsComplexType(parameter.ParameterType);
            
            // Prefer flattening complex types annotated with [FromForm]
            if (fromFormAttribute != null && isComplex)
            {
                AddComplexTypeToSchema(schema, parameter.ParameterType);
            }
            // Handle parameters with [FromForm] attribute for simple types
            else if (fromFormAttribute != null)
            {
                AddParameterToSchema(schema, parameter);
            }
            // Handle complex types that might contain IFormFile properties
            else if (HasFormFileProperty(parameter.ParameterType))
            {
                AddComplexTypeToSchema(schema, parameter.ParameterType);
            }
        }

        // Remove parameters that are now in the request body
        operation.Parameters = operation.Parameters?.Where(p => 
            !parameters.Any(param => param.Name == p.Name)).ToList();
    }

    private void AddParameterToSchema(OpenApiSchema schema, ParameterInfo parameter)
    {
        var parameterSchema = CreateSchemaForType(parameter.ParameterType);
        
        // Handle optional parameters
        if (parameter.HasDefaultValue || IsNullableType(parameter.ParameterType))
        {
            parameterSchema.Nullable = true;
        }
        
        schema.Properties[parameter.Name ?? "unknown"] = parameterSchema;
    }

    private void AddComplexTypeToSchema(OpenApiSchema schema, Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var property in properties)
        {
            var propertySchema = CreateSchemaForType(property.PropertyType);
            
            var isRequired = property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null;

            // Nullable when not required or nullable type
            if (!isRequired || IsNullableType(property.PropertyType))
            {
                propertySchema.Nullable = true;
            }
            else
            {
                // mark as required in the parent schema
                schema.Required ??= new HashSet<string>();
                if (!schema.Required.Contains(property.Name))
                {
                    schema.Required.Add(property.Name);
                }
            }
            
            schema.Properties[property.Name] = propertySchema;
        }
    }

    private OpenApiSchema CreateSchemaForType(Type type)
    {
        if (type == typeof(IFormFile))
        {
            return new OpenApiSchema
            {
                Type = "string",
                Format = "binary"
            };
        }
        
        if (type == typeof(string))
        {
            return new OpenApiSchema { Type = "string" };
        }
        
        if (type == typeof(int) || type == typeof(int?))
        {
            return new OpenApiSchema { Type = "integer", Format = "int32" };
        }
        
        if (type == typeof(bool) || type == typeof(bool?))
        {
            return new OpenApiSchema { Type = "boolean" };
        }
        
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var itemType = type.GetGenericArguments()[0];
            return new OpenApiSchema
            {
                Type = "array",
                Items = CreateSchemaForType(itemType)
            };
        }
        
        // For other complex types, represent as object to avoid string fallback
        if (IsComplexType(type))
        {
            return new OpenApiSchema { Type = "object" };
        }
        
        // Default to string for unknown primitive-like types
        return new OpenApiSchema { Type = "string" };
    }

    private bool HasFormFileProperty(Type type)
    {
        if (type == typeof(IFormFile) || type == typeof(IFormFile[]))
            return true;
            
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var genericArg = type.GetGenericArguments()[0];
            if (genericArg == typeof(IFormFile))
                return true;
        }
        
        // Check if any property is IFormFile
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties.Any(p => p.PropertyType == typeof(IFormFile) || 
                                  p.PropertyType == typeof(IFormFile[]) ||
                                  (p.PropertyType.IsGenericType && 
                                   p.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
                                   p.PropertyType.GetGenericArguments()[0] == typeof(IFormFile)));
    }
    
    private bool IsNullableType(Type type)
    {
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) ||
               !type.IsValueType;
    }

    private bool IsComplexType(Type type)
    {
        if (type == typeof(string)) return false;
        if (type.IsPrimitive) return false;
        if (type == typeof(decimal)) return false;
        if (type == typeof(DateTime) || type == typeof(DateTime?)) return false;
        if (type == typeof(IFormFile)) return false;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) return false;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)) return false;
        return type.IsClass;
    }
}
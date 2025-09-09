# DataValidatorApi Tests

Este proyecto contiene los tests para la API de validación de documentos.

## Problema Resuelto

El proyecto original de tests tenía problemas de compilación debido a conflictos con referencias y dependencias. Se creó este proyecto aislado para resolver estos problemas.

## Estructura de Tests

### Tests Implementados

1. **VisionExtractionServiceTests.cs**
   - Tests básicos para el servicio de extracción con visión artificial
   - Verifica la creación de mocks y funcionalidad básica

2. **AnalysisValidationServiceTests.cs**
   - Tests básicos para el servicio de análisis y validación
   - Verifica la funcionalidad de validación de datos

3. **ValidationControllerTests.cs**
   - Tests básicos para el controlador de validación
   - Verifica las respuestas del API

### Dependencias

- **xUnit**: Framework de testing
- **Moq**: Framework para mocking
- **Microsoft.AspNetCore.Mvc.Testing**: Para tests de integración

## Cómo Ejecutar los Tests

```bash
# Restaurar dependencias
dotnet restore

# Ejecutar todos los tests
dotnet test

# Ejecutar tests con verbosidad
dotnet test --verbosity normal
```

## Estado Actual

✅ **7 tests ejecutándose correctamente**
- Todos los tests pasan sin errores
- Configuración de dependencias funcionando
- Framework de testing configurado correctamente

## Próximos Pasos

Para expandir los tests:

1. Agregar referencia al proyecto principal cuando sea necesario
2. Implementar tests de integración más complejos
3. Agregar tests para casos edge y manejo de errores
4. Implementar tests de performance si es necesario

## Notas Técnicas

- Proyecto configurado para .NET 8.0
- Usa xUnit como framework de testing principal
- Configurado como proyecto de test (`<IsTestProject>true</IsTestProject>`)
- Aislado del proyecto principal para evitar conflictos de dependencias
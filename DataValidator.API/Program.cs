using DataValidator.API.Services;
using DataValidator.Domain.Models;
using DataValidator.Domain.Ports;
using DataValidator.Domain.Services;
using DataValidator.Infrastructure.Processors;
using DataValidator.Infrastructure.Providers.AI;
using Microsoft.OpenApi.Models;
using System.IO;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure AI Models from appsettings.json
builder.Services.Configure<AIModelsConfiguration>(builder.Configuration.GetSection("AIModels"));

// Register a single HttpClient instance to be shared.
// For more advanced scenarios (like setting different base addresses or auth headers per provider),
// consider using named or typed HttpClients with IHttpClientFactory.
builder.Services.AddHttpClient();

// --- Register Application Services ---
// These services orchestrate the business logic.
builder.Services.AddScoped<IVisionExtractionService, VisionExtractionService>();
builder.Services.AddScoped<IAnalysisValidationService, AnalysisValidationService>();
builder.Services.AddScoped<IPromptBuilderService, PromptBuilderService>();

// --- Register Infrastructure Adapters (Ports implementations) ---

// Register the PDF Processor Adapter
builder.Services.AddScoped<IPdfProcessor, PdfProcessorAdapter>();

// Register all available AI Vision Providers.
// The VisionExtractionService will receive an IEnumerable<IAiVisionProvider>
// and select the appropriate one based on configuration.
builder.Services.AddScoped<IAiVisionProvider, OpenAiVisionAdapter>();
builder.Services.AddScoped<IAiVisionProvider, GeminiVisionAdapter>();

// Register all available AI Analysis Providers.
// The AnalysisValidationService will receive an IEnumerable<IAiAnalysisProvider>
// and select the appropriate one based on configuration.
builder.Services.AddScoped<IAiAnalysisProvider, OpenAiAnalysisAdapter>();
builder.Services.AddScoped<IAiAnalysisProvider, GeminiAnalysisAdapter>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger to use XML comments and handle file uploads.
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    // options.IncludeXmlComments(xmlPath); // This might fail if the XML file is not found.

    // Configure Swagger to handle file uploads properly
    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

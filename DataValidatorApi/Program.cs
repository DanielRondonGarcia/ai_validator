using System.IO;
using System.Reflection;
using Microsoft.OpenApi.Models;
using DataValidatorApi.Models;
using DataValidatorApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure AI Models
builder.Services.Configure<AIModelsConfiguration>(builder.Configuration.GetSection("AIModels"));

// Register HttpClient
builder.Services.AddHttpClient();

// Register custom services
builder.Services.AddScoped<IVisionExtractionService, VisionExtractionService>();
builder.Services.AddScoped<IAnalysisValidationService, AnalysisValidationService>();
builder.Services.AddScoped<IPromptBuilderService, PromptBuilderService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger to use XML comments and handle file uploads.
builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    options.IncludeXmlComments(xmlPath);
    
    // Configure Swagger to handle file uploads properly
    options.MapType<IFormFile>(() => new OpenApiSchema
    {
        Type = "string",
        Format = "binary"
    });
    
    // Configure operation filter for multipart/form-data
    options.OperationFilter<FileUploadOperationFilter>();
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

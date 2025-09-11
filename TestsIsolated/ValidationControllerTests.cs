using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using DataValidator.API.Controllers;
using DataValidator.Domain.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using DataValidator.API.Models; // <-- Added this using statement
using AI.Library.Models;

namespace DataValidator.Tests
{
    public class ValidationControllerTests
    {
        private readonly Mock<IConfiguration> _mockConfig;
        private readonly Mock<IVisionExtractionService> _mockVisionService;
        private readonly Mock<IAnalysisValidationService> _mockAnalysisService;
        private readonly Mock<ILogger<ValidationController>> _mockLogger;
        private readonly ValidationController _controller;

        public ValidationControllerTests()
        {
            _mockConfig = new Mock<IConfiguration>();
            _mockVisionService = new Mock<IVisionExtractionService>();
            _mockAnalysisService = new Mock<IAnalysisValidationService>();
            _mockLogger = new Mock<ILogger<ValidationController>>();

            _controller = new ValidationController(
                _mockConfig.Object,
                _mockVisionService.Object,
                _mockAnalysisService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task CrossValidate_ShouldReturnOk_WhenRequestIsValid()
        {
            // Arrange
            var fileMock = new Mock<IFormFile>();
            var content = "Hello World from a Fake File";
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(content);
            writer.Flush();
            ms.Position = 0;
            fileMock.Setup(_ => _.OpenReadStream()).Returns(ms);
            fileMock.Setup(_ => _.FileName).Returns("test.pdf");
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            fileMock.Setup(_ => _.ContentType).Returns("application/pdf");

            var request = new CrossValidationRequest
            {
                File = fileMock.Object,
                JsonData = "{ \"field\": \"value\" }",
                DocumentType = "invoice",
                FieldsToValidate = new List<string> { "field" }
            };

            _mockVisionService.Setup(s => s.ExtractDataFromPdfAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()))
                .ReturnsAsync(new VisionExtractionResult { Success = true, ExtractedData = "Extracted" });
            
            _mockAnalysisService.Setup(s => s.ValidateExtractedDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<string>()))
                .ReturnsAsync(new ValidationAnalysisResult { Success = true, IsValid = true });

            // Act
            var result = await _controller.CrossValidate(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task CrossValidate_ShouldReturnBadRequest_WhenFileIsEmpty()
        {
            // Arrange
            var request = new CrossValidationRequest
            {
                File = null,
                JsonData = "{ \"field\": \"value\" }"
            };

            // Act
            var result = await _controller.CrossValidate(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("File is not provided or empty.", badRequestResult.Value);
        }
    }
}

using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using DataValidator.API.Services;
using DataValidator.Domain.Services;
using DataValidator.Domain.Ports;
using DataValidator.Domain.Models;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using System.Text;

namespace DataValidator.Tests
{
    public class VisionExtractionServiceTests
    {
        private readonly Mock<ILogger<VisionExtractionService>> _mockLogger;
        private readonly Mock<IPromptBuilderService> _mockPromptBuilder;
        private readonly Mock<IOptions<AIModelsConfiguration>> _mockAiConfig;
        private readonly Mock<IPdfProcessor> _mockPdfProcessor;
        private readonly Mock<IAiVisionProvider> _mockPrimaryProvider;
        private readonly Mock<IAiVisionProvider> _mockAlternativeProvider;
        private readonly IEnumerable<IAiVisionProvider> _providers;

        public VisionExtractionServiceTests()
        {
            _mockLogger = new Mock<ILogger<VisionExtractionService>>();
            _mockPromptBuilder = new Mock<IPromptBuilderService>();
            _mockAiConfig = new Mock<IOptions<AIModelsConfiguration>>();
            _mockPdfProcessor = new Mock<IPdfProcessor>();
            _mockPrimaryProvider = new Mock<IAiVisionProvider>();
            _mockAlternativeProvider = new Mock<IAiVisionProvider>();

            var config = new AIModelsConfiguration
            {
                VisionModel = new AIModelConfig { Provider = "Primary" },
                AlternativeVisionModel = new AIModelConfig { Provider = "Alternative" }
            };
            _mockAiConfig.Setup(x => x.Value).Returns(config);

            _mockPrimaryProvider.Setup(p => p.ProviderName).Returns("Primary");
            _mockAlternativeProvider.Setup(p => p.ProviderName).Returns("Alternative");

            _providers = new[] { _mockPrimaryProvider.Object, _mockAlternativeProvider.Object };
        }

        [Fact]
        public async Task ExtractDataFromPdfAsync_ShouldUsePdfProcessor_ForTextBasedPdf()
        {
            // Arrange
            var service = new VisionExtractionService(_mockLogger.Object, _mockPromptBuilder.Object, _mockPdfProcessor.Object, _providers, _mockAiConfig.Object);
            var pdfData = Encoding.UTF8.GetBytes("fake pdf data");
            
            _mockPdfProcessor.Setup(p => p.IsPdfImageBasedAsync(pdfData)).ReturnsAsync(false);
            _mockPdfProcessor.Setup(p => p.ExtractTextAsync(pdfData)).ReturnsAsync("Extracted Text");

            // Act
            var result = await service.ExtractDataFromPdfAsync(pdfData, "test.pdf", "doc", null);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Extracted Text", result.ExtractedData);
            Assert.Equal("PdfPig", result.Provider);
            _mockPdfProcessor.Verify(p => p.ExtractTextAsync(pdfData), Times.Once);
            _mockPrimaryProvider.Verify(p => p.ExtractDataFromImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExtractDataFromPdfAsync_ShouldUseVisionProvider_ForImageBasedPdf()
        {
            // Arrange
            var service = new VisionExtractionService(_mockLogger.Object, _mockPromptBuilder.Object, _mockPdfProcessor.Object, _providers, _mockAiConfig.Object);
            var pdfData = Encoding.UTF8.GetBytes("fake pdf data");
            var imageData = new List<(byte[], int)> { (Encoding.UTF8.GetBytes("fake image"), 1) };

            _mockPdfProcessor.Setup(p => p.IsPdfImageBasedAsync(pdfData)).ReturnsAsync(true);
            _mockPdfProcessor.Setup(p => p.ConvertPdfPagesToImagesAsync(pdfData)).ReturnsAsync(imageData);
            _mockPrimaryProvider.Setup(p => p.ExtractDataFromImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new VisionExtractionResult { Success = true, ExtractedData = "Extracted from image" });

            // Act
            var result = await service.ExtractDataFromPdfAsync(pdfData, "test.pdf", "doc", null);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("Extracted from image", result.ExtractedData);
            _mockPdfProcessor.Verify(p => p.ConvertPdfPagesToImagesAsync(pdfData), Times.Once);
            _mockPrimaryProvider.Verify(p => p.ExtractDataFromImageAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }
}

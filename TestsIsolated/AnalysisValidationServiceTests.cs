using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using DataValidator.API.Services;
using DataValidator.Domain.Services;
using DataValidator.Domain.Ports;
using DataValidator.Domain.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace DataValidator.Tests
{
    public class AnalysisValidationServiceTests
    {
        private readonly Mock<ILogger<AnalysisValidationService>> _mockLogger;
        private readonly Mock<IPromptBuilderService> _mockPromptBuilder;
        private readonly Mock<IOptions<AIModelsConfiguration>> _mockAiConfig;
        private readonly Mock<IAiAnalysisProvider> _mockPrimaryProvider;
        private readonly Mock<IAiAnalysisProvider> _mockAlternativeProvider;

        public AnalysisValidationServiceTests()
        {
            _mockLogger = new Mock<ILogger<AnalysisValidationService>>();
            _mockPromptBuilder = new Mock<IPromptBuilderService>();
            _mockAiConfig = new Mock<IOptions<AIModelsConfiguration>>();
            _mockPrimaryProvider = new Mock<IAiAnalysisProvider>();
            _mockAlternativeProvider = new Mock<IAiAnalysisProvider>();

            // Setup default config
            var config = new AIModelsConfiguration
            {
                AnalysisModel = new AIModelConfig { Provider = "Primary" },
                AlternativeAnalysisModel = new AIModelConfig { Provider = "Alternative" }
            };
            _mockAiConfig.Setup(x => x.Value).Returns(config);

            // Setup default provider names
            _mockPrimaryProvider.Setup(p => p.ProviderName).Returns("Primary");
            _mockAlternativeProvider.Setup(p => p.ProviderName).Returns("Alternative");
        }

        [Fact]
        public async Task ValidateExtractedDataAsync_ShouldCallPrimaryProvider_AndSucceed()
        {
            // Arrange
            var providers = new[] { _mockPrimaryProvider.Object, _mockAlternativeProvider.Object };
            var service = new AnalysisValidationService(_mockLogger.Object, _mockPromptBuilder.Object, providers, _mockAiConfig.Object);

            var successfulAnalysis = @"{ ""isValid"": true, ""confidenceScore"": 0.9, ""analysis"": ""Looks good."" }";
            var providerResult = new ValidationAnalysisResult { Success = true, Analysis = successfulAnalysis };
            _mockPrimaryProvider.Setup(p => p.AnalyzeDataAsync(It.IsAny<string>())).ReturnsAsync(providerResult);

            // Act
            var result = await service.ValidateExtractedDataAsync("some data", "invoice", new List<string>());

            // Assert
            Assert.True(result.Success);
            Assert.True(result.IsValid);
            Assert.Equal(0.9, result.ConfidenceScore);
            _mockPrimaryProvider.Verify(p => p.AnalyzeDataAsync(It.IsAny<string>()), Times.Once);
            _mockAlternativeProvider.Verify(p => p.AnalyzeDataAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidateExtractedDataAsync_ShouldFallBackToAlternative_WhenPrimaryFails()
        {
            // Arrange
            var providers = new[] { _mockPrimaryProvider.Object, _mockAlternativeProvider.Object };
            var service = new AnalysisValidationService(_mockLogger.Object, _mockPromptBuilder.Object, providers, _mockAiConfig.Object);
            
            // Primary provider fails
            var failedProviderResult = new ValidationAnalysisResult { Success = false, ErrorMessage = "Primary failed" };
            _mockPrimaryProvider.Setup(p => p.AnalyzeDataAsync(It.IsAny<string>())).ReturnsAsync(failedProviderResult);

            // Alternative provider succeeds
            var successfulAnalysis = @"{ ""isValid"": true, ""confidenceScore"": 0.8, ""analysis"": ""Alternative looks good."" }";
            var successProviderResult = new ValidationAnalysisResult { Success = true, Analysis = successfulAnalysis, Provider = "Alternative" };
            _mockAlternativeProvider.Setup(p => p.AnalyzeDataAsync(It.IsAny<string>())).ReturnsAsync(successProviderResult);

            // Act
            var result = await service.ValidateExtractedDataAsync("some data", "invoice", new List<string>());

            // Assert
            Assert.True(result.Success);
            Assert.True(result.IsValid);
            Assert.Equal("Alternative", result.Provider);
            _mockPrimaryProvider.Verify(p => p.AnalyzeDataAsync(It.IsAny<string>()), Times.Once);
            _mockAlternativeProvider.Verify(p => p.AnalyzeDataAsync(It.IsAny<string>()), Times.Once);
        }
    }
}

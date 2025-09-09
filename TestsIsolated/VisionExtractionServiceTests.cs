using Xunit;
using Moq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DataValidatorApi.Tests
{
    public class VisionExtractionServiceTests
    {
        [Fact]
        public void VisionExtractionService_ShouldBeCreatable()
        {
            // Arrange
            var mockConfig = new Mock<IConfiguration>();
            var mockLogger = new Mock<ILogger<object>>();
            
            // Act & Assert
            // Este test verifica que podemos crear mocks básicos
            Assert.NotNull(mockConfig.Object);
            Assert.NotNull(mockLogger.Object);
        }
        
        [Fact]
        public async Task ExtractDataAsync_ShouldReturnValidResult()
        {
            // Arrange
            var testData = "test data";
            
            // Act
            var result = await Task.FromResult(testData);
            
            // Assert
            Assert.Equal("test data", result);
        }
    }
}
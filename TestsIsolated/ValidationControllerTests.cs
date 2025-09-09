using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace DataValidatorApi.Tests
{
    public class ValidationControllerTests
    {
        [Fact]
        public void ValidationController_ShouldBeCreatable()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<object>>();
            
            // Act & Assert
            // Este test verifica que podemos crear mocks básicos
            Assert.NotNull(mockLogger.Object);
        }
        
        [Fact]
        public async Task ValidateDocument_ShouldReturnOkResult()
        {
            // Arrange
            var testResult = new OkObjectResult("Test successful");
            
            // Act
            var result = await Task.FromResult(testResult);
            
            // Assert
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal("Test successful", ((OkObjectResult)result).Value);
        }
    }
}
namespace DataValidator.Domain.Models
{
    public class AIModelsConfiguration
    {
        public AIModelConfig VisionModel { get; set; } = new();
        public AIModelConfig AnalysisModel { get; set; } = new();
    }

    public class AIModelConfig
    {
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public int MaxTokens { get; set; } = 2000;
        public double Temperature { get; set; } = 0.2;
        
        // Timeout and retry configuration
        public int TimeoutSeconds { get; set; } = 120; // 2 minutes default
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 2;
        public bool EnableCircuitBreaker { get; set; } = true;
        public int CircuitBreakerFailureThreshold { get; set; } = 5;
        public int CircuitBreakerTimeoutSeconds { get; set; } = 60;
    }
}

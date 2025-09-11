namespace AI.Library.Models
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
    }
}

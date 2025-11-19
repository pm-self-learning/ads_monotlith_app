namespace RetailMonolith.Models
{
    public class AzureOpenAISettings
    {
        public string Endpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty; // (User requested to keep in appsettings)
        public string DeploymentName { get; set; } = string.Empty; // Chat model deployment
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 400;
        public int MaxHistoryMessages { get; set; } = 10; // limit context passed
        public int MaxProductContext { get; set; } = 20; // number of products summarized
    }
}

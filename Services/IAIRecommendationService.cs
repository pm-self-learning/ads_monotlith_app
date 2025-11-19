using RetailMonolith.Models;

namespace RetailMonolith.Services
{
    public interface IAIRecommendationService
    {
        Task<ChatResponse> GetChatResponseAsync(ChatRequest request);
        Task<List<ProductRecommendation>> GetProductRecommendationsAsync(string userMessage, List<Product> availableProducts);
        Task<List<Models.ChatMessage>> GetChatHistoryAsync(string sessionId, int maxMessages = 10);
    }
}
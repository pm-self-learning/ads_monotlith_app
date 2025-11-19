using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RetailMonolith.Data;
using RetailMonolith.Models;
using OpenAI.Chat;
using System.ClientModel;
using ChatApiMessage = OpenAI.Chat.ChatMessage;
using ChatMessageModel = RetailMonolith.Models.ChatMessage;

namespace RetailMonolith.Services
{
    public class AIRecommendationService : IAIRecommendationService
    {
        private readonly AppDbContext _db;
        private readonly ChatClient _chatClient;
        private readonly AzureOpenAISettings _settings;
        private readonly ILogger<AIRecommendationService> _logger;
        private static readonly Regex SkuRegex = new("SKU-\\d{4}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public AIRecommendationService(AppDbContext db,
                                       ChatClient chatClient,
                                       IOptions<AzureOpenAISettings> settings,
                                       ILogger<AIRecommendationService> logger)
        {
            _db = db;
            _chatClient = chatClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<List<ChatMessageModel>> GetChatHistoryAsync(string sessionId, int maxMessages = 10)
        {
            return await _db.ChatMessages
                .Where(m => m.SessionId == sessionId)
                .OrderByDescending(m => m.Timestamp)
                .Take(Math.Min(maxMessages, _settings.MaxHistoryMessages))
                .OrderBy(m => m.Timestamp)
            .Select(m => m) // explicit for clarity
            .ToListAsync();
        }

        public async Task<ChatResponse> GetChatResponseAsync(ChatRequest request)
        {
            var history = await GetChatHistoryAsync(request.SessionId, _settings.MaxHistoryMessages);

            // Build product context (subset)
            var productContext = await _db.Products
                .Where(p => p.IsActive)
                .Take(_settings.MaxProductContext)
                .Select(p => new { p.Sku, p.Name, p.Category, p.Price })
                .ToListAsync();

            var systemPrompt = BuildSystemPrompt(productContext);

            var chatMessages = new List<ChatApiMessage>
            {
                ChatApiMessage.CreateSystemMessage(systemPrompt)
            };

            foreach (var m in history)
            {
                if (m.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
                        chatMessages.Add(ChatApiMessage.CreateUserMessage(m.Content));
                else if (m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                        chatMessages.Add(ChatApiMessage.CreateAssistantMessage(m.Content));
            }

                    chatMessages.Add(ChatApiMessage.CreateUserMessage(request.Message));

            var options = new ChatCompletionOptions
            {
                Temperature = _settings.Temperature,
                MaxOutputTokenCount = _settings.MaxTokens
            };

            ChatCompletion? completion;
            try
            {
                completion = (await _chatClient.CompleteChatAsync(chatMessages, options)).Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Azure OpenAI chat completion failed");
                return new ChatResponse
                {
                    Success = false,
                    Error = "AI service error",
                    SessionId = request.SessionId,
                    Message = "Sorry, I couldn't process that right now."
                };
            }

            var reply = completion.Content.FirstOrDefault()?.Text?.Trim() ?? "(no response)";
            var recommendedSkus = ExtractSkus(reply);
            var recommendedProducts = await _db.Products
                .Where(p => recommendedSkus.Contains(p.Sku))
                .ToListAsync();

            var recommendations = recommendedProducts.Select(p => new ProductRecommendation
            {
                ProductId = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                Reason = $"Recommended based on your interest in {request.Context ?? "shopping"}."
            }).ToList();

            // Persist messages
            _db.ChatMessages.Add(new ChatMessageModel
            {
                SessionId = request.SessionId,
                Role = "user",
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            });
            _db.ChatMessages.Add(new ChatMessageModel
            {
                SessionId = request.SessionId,
                Role = "assistant",
                Content = reply,
                Timestamp = DateTime.UtcNow,
                RecommendedProductIds = recommendations.Any() ? string.Join(',', recommendations.Select(r => r.ProductId)) : null
            });
            await _db.SaveChangesAsync();

            // Log usage
            try
            {
                _logger.LogInformation("Chat completion tokens: input={InputTokens}, output={OutputTokens}",
                    completion.Usage?.InputTokenCount, completion.Usage?.OutputTokenCount);
            }
            catch { /* ignore logging issues */ }

            return new ChatResponse
            {
                SessionId = request.SessionId,
                Message = reply,
                Recommendations = recommendations,
                Success = true
            };
        }

        public async Task<List<ProductRecommendation>> GetProductRecommendationsAsync(string userMessage, List<Product> availableProducts)
        {
            var request = new ChatRequest { Message = userMessage, SessionId = Guid.NewGuid().ToString() };
            var response = await GetChatResponseAsync(request);
            return response.Recommendations ?? new List<ProductRecommendation>();
        }

        private static IEnumerable<string> ExtractSkus(string content)
        {
            return SkuRegex.Matches(content)
                .Select(m => m.Value.ToUpperInvariant())
                .Distinct();
        }

        private string BuildSystemPrompt(IEnumerable<object> productContext)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are a concise retail shopping assistant.");
            sb.AppendLine("Recommend up to 3 products using ONLY the provided catalog.");
            sb.AppendLine("Return product references by SKU (e.g., SKU-0001).");
            sb.AppendLine("If user asks unrelated things, steer back to shopping.");
            sb.AppendLine();
            sb.AppendLine("Catalog:");
            foreach (var p in productContext)
            {
                var skuProp = p.GetType().GetProperty("Sku")?.GetValue(p)?.ToString();
                var nameProp = p.GetType().GetProperty("Name")?.GetValue(p)?.ToString();
                var catProp = p.GetType().GetProperty("Category")?.GetValue(p)?.ToString();
                var priceProp = p.GetType().GetProperty("Price")?.GetValue(p)?.ToString();
                sb.AppendLine($"{skuProp} | {nameProp} | {catProp} | £{priceProp}");
            }
            sb.AppendLine();
            sb.AppendLine("Output: natural helpful reply. Mention SKU codes explicitly when recommending.");
            return sb.ToString();
        }
    }
}
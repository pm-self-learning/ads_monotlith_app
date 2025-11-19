namespace RetailMonolith.Models
{
    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public string? Context { get; set; } // Current page context (e.g., "products", "cart")
    }

    public class ChatResponse
    {
        public string Message { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public List<ProductRecommendation>? Recommendations { get; set; }
        public bool Success { get; set; } = true;
        public string? Error { get; set; }
    }

    public class ProductRecommendation
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public string Reason { get; set; } = string.Empty; // Why this product was recommended
    }
}
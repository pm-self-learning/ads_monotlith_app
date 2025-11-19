using System.ComponentModel.DataAnnotations;

namespace RetailMonolith.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        [Required]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = string.Empty; // "user" or "assistant"

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Optional: Store recommended product IDs
        public string? RecommendedProductIds { get; set; }
    }
}
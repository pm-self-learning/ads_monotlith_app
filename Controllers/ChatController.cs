using Microsoft.AspNetCore.Mvc;
using RetailMonolith.Models;
using RetailMonolith.Services;

namespace RetailMonolith.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IAIRecommendationService _aiService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IAIRecommendationService aiService, ILogger<ChatController> logger)
        {
            _aiService = aiService;
            _logger = logger;
        }

        [HttpPost("message")]
        public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new ChatResponse
                {
                    Success = false,
                    Error = "Message cannot be empty"
                });
            }

            if (string.IsNullOrWhiteSpace(request.SessionId))
            {
                request.SessionId = Guid.NewGuid().ToString();
            }

            try
            {
                var response = await _aiService.GetChatResponseAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat message for session {SessionId}", request.SessionId);
                return StatusCode(500, new ChatResponse
                {
                    Success = false,
                    Error = "Internal server error"
                });
            }
        }

        [HttpGet("history/{sessionId}")]
        public async Task<ActionResult<List<Models.ChatMessage>>> GetChatHistory(string sessionId, [FromQuery] int maxMessages = 10)
        {
            try
            {
                var history = await _aiService.GetChatHistoryAsync(sessionId, maxMessages);
                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving chat history for session {SessionId}", sessionId);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("recommendations")]
        public async Task<ActionResult<List<ProductRecommendation>>> GetRecommendations([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty");
            }

            try
            {
                // Reuse chat logic for recommendations only
                var resp = await _aiService.GetChatResponseAsync(request);
                return Ok(resp.Recommendations ?? new List<ProductRecommendation>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations");
                return StatusCode(500, "Internal server error");
            }
        }
    }
}
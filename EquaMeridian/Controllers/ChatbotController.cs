using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[AllowAnonymous]
[Route("api/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbot;

    public ChatbotController(IChatbotService chatbot) => _chatbot = chatbot;

    /// <summary>Ask the self-trained EquaMeridian intent classifier.</summary>
    [HttpPost("ask")]
    public async Task<IActionResult> Ask([FromBody] ChatbotAskDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        int? userId = User.Identity?.IsAuthenticated == true
            && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
        var role = User.FindFirstValue(ClaimTypes.Role);

        var reply = await _chatbot.AskAsync(dto.Message.Trim(), userId, role, dto.SessionId);
        return Ok(reply);
    }

    /// <summary>
    /// Model card for demos / markers: algorithm, training size, holdout accuracy, intent list.
    /// Proves the bot is self-trained ML.NET, not a hard-coded if/else FAQ only.
    /// </summary>
    [HttpGet("model-info")]
    public IActionResult ModelInfo() => Ok(_chatbot.GetModelInfo());

    /// <summary>User feedback on a reply (helpful / not) for continuous improvement demos.</summary>
    [HttpPost("feedback")]
    public IActionResult Feedback([FromBody] ChatbotFeedbackDto dto)
    {
        if (dto is null) return BadRequest();
        _chatbot.RecordFeedback(dto);
        return Ok(new { success = true });
    }

    [HttpGet("feedback-summary")]
    [Authorize(Roles = "Admin")]
    public IActionResult FeedbackSummary() => Ok(_chatbot.GetFeedbackSummary());
}

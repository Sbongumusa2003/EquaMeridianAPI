using EquaMeridian.DTOs.Messages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly IMessageRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public MessagesController(IMessageRepository repo, IAuditService audit, IEmailService email,
        INotificationRepository notifications)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _notifications = notifications;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("threads")]
    public async Task<IActionResult> GetThreads(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (threads, total) = await _repo.GetThreadsForUserAsync(UserId, search, page, pageSize);
        return Ok(new { threads, totalCount = total, page, pageSize });
    }

    [HttpGet("threads/{threadId}")]
    public async Task<IActionResult> GetThreadDetail(int threadId)
    {
        var thread = await _repo.GetThreadDetailAsync(threadId, UserId);
        return thread == null ? NotFound(new { message = "Conversation not found." }) : Ok(thread);
    }

    [HttpGet("recipients")]
    public async Task<IActionResult> SearchRecipients([FromQuery] string search)
    {
        var results = await _repo.SearchRecipientsAsync(UserId, search);
        return Ok(results);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Send([FromForm] SendMessageDto dto, IFormFile? attachment)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.SendAsync(UserId, dto, attachment);
        if (!result.Success)
        {
            return result.ErrorCode == "NotFound"
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(UserId, "MESSAGE_SENT",
            $"Message sent to user {dto.RecipientID} in thread {result.Message!.ThreadID}", UserId, null, null, ip);

        await _email.SendNewMessageEmailAsync(
            result.RecipientEmail, result.RecipientName, result.SenderName, result.Message!.ThreadID);

        await _notifications.CreateAsync(dto.RecipientID, "NewMessage",
            $"New message from {result.SenderName}", result.Message!.Body,
            "Thread", result.Message!.ThreadID, emailUser: false);
        return CreatedAtAction(nameof(GetThreadDetail), new { threadId = result.Message!.ThreadID }, result.Message);
    }

    [HttpPost("threads/{threadId}/reply")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Reply(int threadId, [FromForm] ReplyMessageDto dto, IFormFile? attachment)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.ReplyAsync(UserId, threadId, dto, attachment);
        if (!result.Success)
        {
            return result.ErrorCode == "NotFound"
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(UserId, "MESSAGE_SENT",
            $"Reply sent in thread {threadId}", UserId, null, null, ip);

        await _email.SendNewMessageEmailAsync(
            result.RecipientEmail, result.RecipientName, result.SenderName, threadId);

        await _notifications.CreateAsync(result.Message!.RecipientID, "NewMessage",
            $"New message from {result.SenderName}", result.Message!.Body,
            "Thread", threadId, emailUser: false);
        return Ok(result.Message);
    }
}

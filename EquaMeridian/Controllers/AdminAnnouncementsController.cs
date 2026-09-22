using EquaMeridian.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;


[ApiController]
[Route("api/admin/announcements")]
[Authorize(Policy = "AdminOnly")]
public class AdminAnnouncementsController : ControllerBase
{
    private readonly INotificationRepository _repo;
    private readonly IAuditService _audit;

    public AdminAnnouncementsController(INotificationRepository repo, IAuditService audit)
    {
        _repo = repo;
        _audit = audit;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var recipientCount = await _repo.BroadcastAsync(dto.Title, dto.Body, dto.TargetRole);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(AdminId, "ANNOUNCEMENT_SENT",
            $"Announcement \"{dto.Title}\" sent to {recipientCount} user(s)" +
            (string.IsNullOrWhiteSpace(dto.TargetRole) ? "" : $" (role: {dto.TargetRole})"),
            AdminId, null, null, ip);

        return Ok(new AnnouncementResultDto
        {
            RecipientCount = recipientCount,
            Title = dto.Title,
            SentDate = AppTime.Now
        });
    }
}

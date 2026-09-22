using EquaMeridian.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _repo;
    public NotificationsController(INotificationRepository repo) => _repo = repo;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] bool? unreadOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _repo.GetForUserAsync(UserId, unreadOnly, page, pageSize);
        return Ok(result);
    }

    [HttpPatch("{notificationId}/read")]
    public async Task<IActionResult> MarkRead(int notificationId)
    {
        var found = await _repo.MarkReadAsync(notificationId, UserId);
        return found ? NoContent() : NotFound(new { message = "Notification not found." });
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var count = await _repo.MarkAllReadAsync(UserId);
        return Ok(new { updated = count });
    }
}

using EquaMeridian.DTOs.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/reviews")]
[Authorize(Policy = "AdminOnly")]
public class AdminReviewsController : ControllerBase
{
    private readonly IReviewRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public AdminReviewsController(IReviewRepository repo, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? listingId,
        [FromQuery] int? starFilter,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _repo.GetAllForAdminAsync(page, pageSize, status, listingId, starFilter, search);
        return Ok(result);
    }

    [HttpDelete("{reviewId}")]
    public async Task<IActionResult> Delete(int reviewId, [FromBody] AdminDeleteReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.AdminDeleteAsync(reviewId, AdminId, dto.Reason);

        if (!result.Success)
            return result.ErrorType switch
            {
                "NotFound" => NotFound(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error })
            };

        await _audit.LogAsync(result.ContractorID, "Review_Admin_Deleted",
            $"Admin removed review #{reviewId}: {dto.Reason}",
            AdminId, null, "Published", ip, "Deleted");

        await _email.SendReviewDeletedEmailAsync(
            result.SupplierEmail, result.SupplierName, reviewId, result.Machinery);

        await _notifications.CreateAsync(result.ContractorID, "ReviewAdminDeleted",
            "Your Review Was Removed",
            $"Your review for \"{result.Machinery}\" was removed by an administrator. Reason: {dto.Reason}", emailUser: false);
        return Ok(new { message = "Review removed." });
    }
}

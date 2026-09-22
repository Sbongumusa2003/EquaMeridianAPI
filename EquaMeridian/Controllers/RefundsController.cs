using EquaMeridian.DTOs.Refunds;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/admin/refunds")]
[Authorize(Policy = "AdminOnly")]
public class RefundsController : ControllerBase
{
    private readonly IRefundRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;
    private readonly AppDbContext _db;

    public RefundsController(
        IRefundRepository repo,
        IAuditService audit,
        IEmailService email,
        INotificationRepository notifications,
        AppDbContext db)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _notifications = notifications;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (refunds, total) = await _repo.GetAllAsync(search, status, page, pageSize);
        return Ok(new { refunds, totalCount = total, page, pageSize });
    }

    [HttpGet("{refundId}")]
    public async Task<IActionResult> GetById(int refundId)
    {
        var refund = await _repo.GetByIdAsync(refundId);
        return refund == null ? NotFound() : Ok(refund);
    }

    [HttpPatch("{refundId}/status")]
    public async Task<IActionResult> ProcessRefund(int refundId, [FromBody] ProcessRefundDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.ProcessAsync(refundId, dto.NewStatus, dto.AdministratorNotes, dto.FailureReason);
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "NotFound" => NotFound(new { message = result.Error }),
                "AlreadyProcessed" => Conflict(new { message = result.Error }),
                "GatewayError" => BadRequest(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error })
            };
        }

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(adminId, "REFUND_PROCESSED",
            $"Refund #{refundId} marked as {dto.NewStatus}.",
            adminId, null, "Pending", ip, dto.NewStatus);

        var refund = result.Refund!;
        var requester = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserID == refund.RequestedByUserID);
        if (requester != null && !string.IsNullOrWhiteSpace(requester.Email))
        {
            var approved = dto.NewStatus == "Processed";
            var title = approved ? "Refund approved" : "Refund declined";
            var body = approved
                ? $"Your refund request #{refund.RefundID} for R {refund.Amount:N2} has been processed."
                : $"Your refund request #{refund.RefundID} was declined. Reason: {dto.FailureReason ?? "See notes from admin."}";

            await _notifications.CreateAsync(requester.UserID, "RefundUpdate", title, body,
                "Refund", refund.RefundID, emailUser: false);
            await _email.SendNotificationEmailAsync(
                requester.Email, requester.FullName, title,
                $"Hi {requester.FullName},<br><br>{body}<br><br>Log in to view payment history for details.",
                "RefundUpdate");
        }

        return Ok(result.Refund);
    }
}

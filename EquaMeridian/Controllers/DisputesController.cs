using EquaMeridian.DTOs.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/admin/disputes")]
[Authorize(Policy = "AdminOnly")]
public class DisputesController : ControllerBase
{
    private static readonly HashSet<string> ValidOutcomes = new()
    {
        "UpholdRefund", "PartialRefund", "Reject", "Escalate"
    };

    private readonly IDisputeRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public DisputesController(IDisputeRepository repo, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (disputes, total) = await _repo.GetAllAsync(search, status, page, pageSize);
        return Ok(new { disputes, totalCount = total, page, pageSize });
    }

    [HttpGet("{disputeId}")]
    public async Task<IActionResult> GetById(int disputeId)
    {
        var dispute = await _repo.GetForReviewAsync(disputeId);
        return dispute == null ? NotFound() : Ok(dispute);
    }

    [HttpPost("{disputeId}/resolve")]
    public async Task<IActionResult> Resolve(int disputeId, [FromBody] ResolveDisputeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!ValidOutcomes.Contains(dto.ResolutionOutcome))
            return BadRequest(new { message = "Resolution outcome must be one of: UpholdRefund, PartialRefund, Reject, Escalate." });

        if (string.IsNullOrWhiteSpace(dto.ResolutionNotes))
            return BadRequest(new { message = "Resolution notes are required." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _repo.ResolveAsync(disputeId, dto, adminId);

        if (!result.Success)
            return result.Error == "Dispute not found." ? NotFound() : BadRequest(new { message = result.Error });

        await _audit.LogAsync(null, "DISPUTE_RESOLVED",
            $"Dispute #{disputeId} resolved with outcome '{dto.ResolutionOutcome}'.",
            adminId, null, result.PreviousStatus,
            ip, JsonSerializer.Serialize(new { dto.ResolutionOutcome, result.Dispute!.Status }));

        await _email.SendDisputeResolutionEmailAsync(
            result.ContractorEmail, result.ContractorName, disputeId, dto.ResolutionOutcome, dto.ResolutionNotes);
        await _email.SendDisputeResolutionEmailAsync(
            result.SupplierEmail, result.SupplierName, disputeId, dto.ResolutionOutcome, dto.ResolutionNotes);

        await _notifications.CreateAsync(result.ContractorID, "DisputeResolved",
            "Dispute Resolved",
            $"Dispute #{disputeId} has been resolved: {dto.ResolutionOutcome}.",
            "Dispute", disputeId, emailUser: false);        await _notifications.CreateAsync(result.SupplierID, "DisputeResolved",
            "Dispute Resolved",
            $"Dispute #{disputeId} has been resolved: {dto.ResolutionOutcome}.",
            "Dispute", disputeId, emailUser: false);
        return Ok(new { dispute = result.Dispute, refundId = result.CreatedRefundId });
    }
}

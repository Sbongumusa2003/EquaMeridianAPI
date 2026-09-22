using EquaMeridian.DTOs.Inspections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/admin/inspections")]
[Authorize(Policy = "AdminOnly")]
public class InspectionsController : ControllerBase
{
    private readonly IInspectionRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public InspectionsController(IInspectionRepository repo, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (inspections, total) = await _repo.GetAllAsync(status, page, pageSize);
        return Ok(new { inspections, totalCount = total, page, pageSize });
    }
    [HttpGet("{inspectionId}")]
    public async Task<IActionResult> GetById(int inspectionId)
    {
        var inspection = await _repo.GetByIdAsync(inspectionId);
        return inspection == null ? NotFound() : Ok(inspection);
    }

    [HttpGet("machinery")]
    public async Task<IActionResult> GetAvailableMachinery()
    {
        var machinery = await _repo.GetAvailableMachineryAsync();
        return Ok(machinery);
    }

    [HttpPost]
    public async Task<IActionResult> RequestInspection([FromBody] RequestInspectionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.RequestAsync(dto, AdminId);

        if (!result.Success)
            return result.Error == "Machinery listing not found." ? NotFound() : BadRequest(new { message = result.Error });

        var inspection = result.Inspection!;

        await _audit.LogAsync(AdminId, "INSPECTION_REQUESTED",
            $"Inspection requested for listing #{dto.ListingID}, scheduled {dto.ScheduledDate:d}.",
            AdminId, dto.ListingID, null, ip,
            JsonSerializer.Serialize(new { inspection.InspectionID, inspection.Status }));

        await _email.SendInspectionRequestedEmailAsync(
            result.SupplierEmail, result.SupplierName, inspection.InspectionID,
            inspection.MachineryTitle, dto.ScheduledDate);

        await _notifications.CreateAsync(result.SupplierID, "InspectionRequested",
            "Inspection requested",
            $"An inspection has been requested for \"{inspection.MachineryTitle}\", scheduled {dto.ScheduledDate:d}.",
            "Inspection", inspection.InspectionID, emailUser: false);

        return Ok(new { inspection });
    }

    [HttpGet("{inspectionId}/outcome")]
    public async Task<IActionResult> GetForOutcome(int inspectionId)
    {
        var inspection = await _repo.GetForOutcomeAsAdminAsync(inspectionId);
        return inspection == null ? NotFound() : Ok(inspection);
    }

    // Req: admin can confirm the outcome of any inspection (the supplier being inspected cannot).
    [HttpPost("{inspectionId}/confirm")]
    public async Task<IActionResult> Confirm(int inspectionId, [FromBody] ConfirmOutcomeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.ConfirmOutcomeAsAdminAsync(inspectionId, AdminId, dto);

        if (!result.Success)
            return result.Error == "Inspection not found." ? NotFound() : BadRequest(new { message = result.Error });

        var inspection = result.Inspection!;

        await _audit.LogAsync(AdminId, "INSPECTION_OUTCOME_CONFIRMED",
            $"Inspection #{inspectionId} outcome confirmed as '{dto.Outcome}' by admin.",
            AdminId, inspection.ListingID, null, ip,
            JsonSerializer.Serialize(new { inspection.Status, inspection.Outcome }));

        // Dedicated emails to both requester and supplier (role-aware greeting).
        await _email.SendInspectionOutcomeConfirmedEmailAsync(
            result.RequesterEmail, result.RequesterName, inspectionId,
            inspection.MachineryTitle, dto.Outcome);

        if (!string.Equals(result.RequesterEmail, result.SupplierEmail, StringComparison.OrdinalIgnoreCase))
        {
            await _email.SendInspectionOutcomeConfirmedEmailAsync(
                result.SupplierEmail, result.SupplierName, inspectionId,
                inspection.MachineryTitle, dto.Outcome);
        }

        await _notifications.CreateAsync(result.RequesterID, "InspectionOutcomeConfirmed",
            "Inspection outcome confirmed",
            $"Inspection #{inspectionId} for \"{inspection.MachineryTitle}\" was confirmed as {dto.Outcome}.",
            "Inspection", inspectionId, emailUser: false);

        await _notifications.CreateAsync(result.SupplierID, "InspectionOutcomeConfirmed",
            "Inspection outcome recorded",
            $"The inspection for \"{inspection.MachineryTitle}\" (#{inspectionId}) has been confirmed as {dto.Outcome}.",
            "Inspection", inspectionId, emailUser: false);

        return Ok(new { inspection });
    }
}

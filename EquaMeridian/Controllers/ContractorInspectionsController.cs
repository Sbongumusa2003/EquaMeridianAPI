using EquaMeridian.DTOs.Inspections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/contractor/inspections")]
[Authorize(Policy = "ContractorOnly")]
public class ContractorInspectionsController : ControllerBase
{
    private readonly IInspectionRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;
    private readonly IConfiguration _config;

    public ContractorInspectionsController(
        IInspectionRepository repo,
        IAuditService audit,
        IEmailService email,
        INotificationRepository notifications,
        IConfiguration config)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _notifications = notifications;
        _config = config;
    }

    private int ContractorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string ContractorName =>
        User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue("name")
        ?? "Contractor";

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (inspections, total) = await _repo.GetAllForContractorAsync(ContractorId, status, page, pageSize);
        return Ok(new { inspections, totalCount = total, page, pageSize });
    }

    [HttpGet("machinery")]
    public async Task<IActionResult> GetAvailableMachinery()
    {
        var machinery = await _repo.GetActiveMachineryAsync();
        return Ok(machinery);
    }

    [HttpPost]
    public async Task<IActionResult> RequestInspection([FromBody] RequestInspectionDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.RequestAsync(dto, ContractorId);

        if (!result.Success)
            return result.Error == "Machinery listing not found." ? NotFound(new { message = result.Error }) : BadRequest(new { message = result.Error });

        var inspection = result.Inspection!;

        await _audit.LogAsync(ContractorId, "INSPECTION_REQUESTED",
            $"Contractor requested inspection for listing #{dto.ListingID}, scheduled {dto.ScheduledDate:d}.",
            null, dto.ListingID, null, ip,
            JsonSerializer.Serialize(new { inspection.InspectionID, inspection.Status }));

        await _email.SendInspectionRequestedEmailAsync(
            result.SupplierEmail, result.SupplierName, inspection.InspectionID,
            inspection.MachineryTitle, dto.ScheduledDate);

        // Notify platform admin so contractor-initiated inspections are visible offline.
        var adminEmail = _config["AdminEmail"] ?? "admin@equameridian.co.za";
        await _email.SendInspectionRequestedAdminEmailAsync(
            adminEmail, inspection.InspectionID, inspection.MachineryTitle,
            dto.ScheduledDate, ContractorName);

        await _notifications.CreateAsync(result.SupplierID, "InspectionRequested",
            "Inspection requested",
            $"An inspection has been requested for \"{inspection.MachineryTitle}\", scheduled {dto.ScheduledDate:d}.",
            "Inspection", inspection.InspectionID, emailUser: false);

        return Ok(new { inspection });
    }

    [HttpGet("{inspectionId}/outcome")]
    public async Task<IActionResult> GetForOutcome(int inspectionId)
    {
        // Scoped to inspections THIS contractor requested — see IInspectionRepository for why.
        var inspection = await _repo.GetForOutcomeAsContractorAsync(inspectionId, ContractorId);
        return inspection == null ? NotFound() : Ok(inspection);
    }

    // Req: a contractor may confirm the outcome of an inspection only if they requested it
    // themselves — not any inspection on any listing. The supplier cannot confirm at all.
    [HttpPost("{inspectionId}/confirm")]
    public async Task<IActionResult> Confirm(int inspectionId, [FromBody] ConfirmOutcomeDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.ConfirmOutcomeAsContractorAsync(inspectionId, ContractorId, dto);

        if (!result.Success)
            return result.Error == "Inspection not found." ? NotFound(new { message = result.Error }) : BadRequest(new { message = result.Error });

        var inspection = result.Inspection!;

        await _audit.LogAsync(ContractorId, "INSPECTION_OUTCOME_CONFIRMED",
            $"Inspection #{inspectionId} outcome confirmed as '{dto.Outcome}' by requesting contractor.",
            null, inspection.ListingID, null, ip,
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

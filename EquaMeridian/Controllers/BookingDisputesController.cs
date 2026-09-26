using EquaMeridian.DTOs.Disputes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/bookings-deliveries/{bookingId}/disputes")]
[Authorize]
public class BookingDisputesController : ControllerBase
{
    private readonly IDisputeRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly INotificationRepository _notifications;

    public BookingDisputesController(
        IDisputeRepository repo, IAuditService audit, IEmailService email, IConfiguration config, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _config = config; _notifications = notifications; }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Raise(
        int bookingId, [FromForm] RaiseDisputeDto dto, List<IFormFile>? evidence)
    {
        if (!Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
            && !Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        if (!ModelState.IsValid) return BadRequest(ModelState);

        List<string> evidencePaths;
        try
        {
            evidencePaths = await SaveEvidenceAsync(bookingId, evidence);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.RaiseAsync(bookingId, UserId, Role, dto, evidencePaths);

        if (!result.Success)
        {
            return result.Error == "Booking not found."
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }

        await _audit.LogAsync(UserId, "DISPUTE_RAISED",
            $"{Role} raised a dispute (#{result.Dispute!.DisputeID}) against booking #{bookingId}.",
            null, null, null, ip,
            JsonSerializer.Serialize(new { dto.DisputeCategory, result.Dispute!.DisputeID }));
        var adminEmail = _config["AdminEmail"] ?? "admin@equameridian.co.za";
        await _email.SendDisputeRaisedEmailAsync(
            adminEmail, "Admin Dispute Resolution Team", result.Dispute.DisputeID, bookingId, dto.DisputeCategory);
        await _email.SendDisputeRaisedEmailAsync(
            result.RespondentEmail, result.RespondentName, result.Dispute.DisputeID, bookingId, dto.DisputeCategory);

        if (result.RespondentID.HasValue)
        {
            await _notifications.CreateAsync(result.RespondentID.Value, "DisputeRaised",
                "Dispute Raised Against You",
                $"{result.ComplainantName} raised a dispute (#{result.Dispute.DisputeID}) regarding booking #{bookingId}.",
                "Dispute", result.Dispute.DisputeID, emailUser: false);        }

        return Ok(new
        {
            message = "Dispute submitted successfully. Our team will review it shortly.",
            dispute = result.Dispute
        });
    }
    private async Task<List<string>> SaveEvidenceAsync(int bookingId, List<IFormFile>? evidence)
    {
        var paths = new List<string>();
        if (evidence == null || evidence.Count == 0) return paths;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
        const long maxFileSizeBytes = 10 * 1024 * 1024;

        var uploadPath = Path.Combine(
            Directory.GetCurrentDirectory(), "uploads", "dispute-evidence", bookingId.ToString());
        Directory.CreateDirectory(uploadPath);

        foreach (var file in evidence)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                throw new ArgumentException("Supporting evidence must be an image (JPG/PNG) or PDF.");
            if (file.Length > maxFileSizeBytes)
                throw new ArgumentException("Each supporting evidence file must be 10MB or smaller.");

            var storedFileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadPath, storedFileName);

            using (var stream = System.IO.File.Create(fullPath))
                await file.CopyToAsync(stream);

            paths.Add(fullPath);
        }

        return paths;
    }
}

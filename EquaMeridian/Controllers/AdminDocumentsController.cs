using EquaMeridian.DTOs.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/admin/documents")]
[Authorize(Policy = "AdminOnly")]
public class AdminDocumentsController : ControllerBase
{
    private static readonly HashSet<string> ValidDecisions = new() { "Accepted", "Rejected" };

    private readonly IDocumentRepository _repo;
    private readonly IUserRepository _users;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public AdminDocumentsController(IDocumentRepository repo, IUserRepository users, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _users = users; _audit = audit; _email = email; _notifications = notifications; }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (documents, total) = await _repo.GetForAdminReviewAsync(status, userId, page, pageSize);
        return Ok(new { documents, totalCount = total, page, pageSize });
    }

    [HttpGet("{docId}")]
    public async Task<IActionResult> GetById(int docId)
    {
        var doc = await _repo.GetByIdForAdminAsync(docId);
        return doc == null ? NotFound() : Ok(doc);
    }

    /// <summary>
    /// Streams the document's bytes for admin viewing. Content lives in Postgres (durable
    /// across Render redeploys); do not rely on the legacy /uploads static path for this.
    /// </summary>
    [HttpGet("{docId}/file")]
    public async Task<IActionResult> GetFile(int docId)
    {
        var result = await _repo.GetContentForAdminAsync(docId);
        if (result is null)
            return NotFound(new { message = "This document's file could not be found. It may need to be re-uploaded." });

        var (content, contentType, docName) = result.Value;
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{docName}\"";
        return File(content, contentType);
    }

    [HttpGet("checklist/{userId}")]
    public async Task<IActionResult> GetChecklist(int userId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();

        var checklist = await _repo.GetDocumentChecklistAsync(userId, user.Role);
        var allRequiredApproved = checklist.Where(c => c.IsRequired).All(c => c.Status == "Accepted");

        return Ok(new { checklist, allRequiredApproved });
    }

    [HttpPost("{docId}/review")]
    public async Task<IActionResult> Review(int docId, [FromBody] ReviewDocumentDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!ValidDecisions.Contains(dto.Decision))
            return BadRequest(new { message = "Decision must be Accepted or Rejected." });

        if (dto.Decision == "Rejected" && string.IsNullOrWhiteSpace(dto.Reason))
            return BadRequest(new { message = "Please provide a reason for rejecting this document." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.ReviewAsync(docId, dto, AdminId);

        if (!result.Success)
            return result.Error == "Document not found." ? NotFound() : BadRequest(new { message = result.Error });

        var doc = result.Document!;

        await _audit.LogAsync(null, "DOCUMENT_REVIEWED",
            $"Document #{docId} ('{doc.DocName}') {dto.Decision.ToLowerInvariant()}" +
            (dto.Decision == "Rejected" ? $" — reason: {dto.Reason}" : "") + ".",
            AdminId, null, "Pending", ip,
            JsonSerializer.Serialize(new { doc.VerificationStatus, doc.RejectionReason }));

        await _email.SendDocumentReviewedEmailAsync(
            result.OwnerEmail, result.OwnerName, docId, doc.DocName, dto.Decision, dto.Reason);

        await _notifications.CreateAsync(result.OwnerID,
            dto.Decision == "Accepted" ? "DocumentAccepted" : "DocumentRejected",
            dto.Decision == "Accepted" ? "Document Approved" : "Document Rejected",
            dto.Decision == "Accepted"
                ? $"Your document '{doc.DocName}' was approved."
                : $"Your document '{doc.DocName}' was rejected: {dto.Reason}",
            "Document", docId, emailUser: false);
        if (result.AccountActivated)
        {
            await _audit.LogAsync(null, "ACCOUNT_STATUS_UPDATED",
                $"Account activated after document #{docId} approval (all required documents approved).",
                AdminId, null, "Pending", ip, "Active");

            await _email.SendAccountStatusChangedAsync(result.OwnerEmail, result.OwnerName, "Active");
            await _notifications.CreateAsync(result.OwnerID, "AccountActivated",
                "Account Activated", "All your required documents have been approved. Your account is now active.", emailUser: false);        }

        return Ok(new { document = doc, accountActivated = result.AccountActivated });
    }
}

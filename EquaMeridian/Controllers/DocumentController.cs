using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/users/me/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly IDocumentRepository _repo;
    private readonly IAuditService _audit;
    private readonly AppDbContext _db;
    private readonly INotificationRepository _notifications;

    public DocumentController(IDocumentRepository repo, IAuditService audit, AppDbContext db, INotificationRepository notifications)
    {
        _repo = repo;
        _audit = audit;
        _db = db;
        _notifications = notifications;
    }

    private int UserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [AllowAnonymous]
    [HttpGet("types")]
    public async Task<IActionResult> GetTypes([FromQuery] string? role = null)
    {
        var effectiveRole = role ?? User.FindFirstValue(ClaimTypes.Role);

        var types = await _db.DocumentTypes
            .Where(t => effectiveRole == null || t.AppliesToRole == null || t.AppliesToRole == effectiveRole)
            .ToListAsync();
        return Ok(types);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyDocuments()
    {
        // Return a flat DTO so JSON never tries to serialise EF navigation properties
        // (User / DocType), which previously caused client-side "conflict"/parse failures.
        var docs = await _repo.GetByUserDtoAsync(UserId);
        return Ok(docs);
    }

    [HttpPost]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Upload([FromForm] int docTypeId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please add a document." });

        try
        {
            var docId = await _repo.UploadAsync(UserId, docTypeId, file);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _audit.LogAsync(UserId, "DOCUMENT_UPLOADED",
                $"Document '{file.FileName}' uploaded", null, null, null, ip);

            var uploaderName = User.FindFirstValue(ClaimTypes.Name) ?? "A user";
            await _notifications.BroadcastAsync(
                "New Document Pending Review",
                $"{uploaderName} uploaded a verification document that requires review.",
                "Admin", "DocumentPendingReview", "Document", docId);

            return Ok(new { message = "Document successfully added.", docId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{docId}")]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Replace(int docId, [FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please add a document." });

        var (success, message) = await _repo.ReplaceAsync(UserId, docId, file);

        if (!success)
        {
            if (message == "Not found.") return NotFound();
            return BadRequest(new { message });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(UserId, "DOCUMENT_REPLACED",
            $"Document {docId} replaced with '{file.FileName}'", null, null, null, ip);

        var uploaderName = User.FindFirstValue(ClaimTypes.Name) ?? "A user";
        await _notifications.BroadcastAsync(
            "Document Re-uploaded - Pending Review",
            $"{uploaderName} replaced a verification document, which now requires review.",
            "Admin", "DocumentPendingReview", "Document", docId);

        return Ok(new { message, docId });
    }
}

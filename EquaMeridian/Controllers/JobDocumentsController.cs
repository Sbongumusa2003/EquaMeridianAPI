using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using System.Security.Claims;

[ApiController]
[Authorize]
public class JobDocumentsController : ControllerBase
{
    private readonly IJobDocumentRepository _repo;
    private readonly IAuditService _audit;

    public JobDocumentsController(IJobDocumentRepository repo, IAuditService audit)
    { _repo = repo; _audit = audit; }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet("api/lease-agreements/{leaseAgreementId}/documents")]
    public async Task<IActionResult> GetForAgreement(
        int leaseAgreementId, [FromQuery] string? documentType, [FromQuery] string? search)
    {
        var page = await _repo.GetForAgreementAsync(leaseAgreementId, UserId, Role, documentType, search);
        if (page == null)
            return NotFound(new { message = "You do not have permission to view documents for this agreement." });

        var hasAny = page.InspectionReports.Count + page.JobSitePhotos.Count
            + page.DeliveryDocumentation.Count + page.MaintenanceRecords.Count + page.OtherDocuments.Count > 0;

        return Ok(new
        {
            page,
            message = hasAny ? null : "No job documents have been uploaded for this agreement yet."
        });
    }

    [HttpGet("api/job-documents/{jobDocumentId}")]
    public async Task<IActionResult> View(int jobDocumentId)
    {
        var doc = await _repo.GetDocumentAsync(jobDocumentId, UserId, Role);
        if (doc == null)
            return NotFound(new { message = "You do not have permission to view this document." });

        if (!System.IO.File.Exists(doc.FilePath))
            return NotFound(new { message = "Unable to view document. The file may be corrupted or inaccessible. Please try downloading the document instead." });

        await _audit.LogAsync(UserId, "JOB_DOCUMENT_VIEWED",
            $"User viewed job document '{doc.DocumentName}' (#{doc.JobDocumentID}).",
            null, null, null, Ip);

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(doc.FilePath, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(doc.FilePath, contentType);
    }

    [HttpGet("api/job-documents/{jobDocumentId}/download")]
    public async Task<IActionResult> Download(int jobDocumentId)
    {
        var doc = await _repo.GetDocumentAsync(jobDocumentId, UserId, Role);
        if (doc == null)
            return NotFound(new { message = "You do not have permission to view this document." });

        if (!System.IO.File.Exists(doc.FilePath))
            return NotFound(new { message = "Unable to view document. The file may be corrupted or inaccessible. Please try downloading the document instead." });

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(doc.FilePath, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(doc.FilePath, contentType, doc.DocumentName);
    }

    [HttpPost("api/lease-agreements/{leaseAgreementId}/documents")]
    public async Task<IActionResult> Upload(int leaseAgreementId, [FromForm] string documentType, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please add a document." });

        try
        {
            var doc = await _repo.UploadAsync(leaseAgreementId, UserId, Role, documentType, file);
            if (doc == null)
                return NotFound(new { message = "You do not have permission to upload documents for this agreement." });

            await _audit.LogAsync(UserId, "JOB_DOCUMENT_UPLOADED",
                $"Document '{file.FileName}' uploaded for lease agreement #{leaseAgreementId}.",
                null, null, null, Ip);

            return Ok(new { message = "Document successfully added.", document = doc });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

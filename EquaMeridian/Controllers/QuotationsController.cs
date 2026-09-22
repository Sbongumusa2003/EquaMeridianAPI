using EquaMeridian.DTOs.Quotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/supplier/quotations")]
[Authorize(Policy = "SupplierOnly")]
public class QuotationsController : ControllerBase
{
    private readonly IQuotationRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public QuotationsController(IQuotationRepository repo, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    private int SupplierId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? listingId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (quotations, total) = await _repo.GetAllForSupplierAsync(SupplierId, listingId, status, page, pageSize);
        return Ok(new { quotations, totalCount = total, page, pageSize });
    }

    [HttpGet("{quotationId}/review")]
    public async Task<IActionResult> GetForReview(int quotationId)
    {
        var quotation = await _repo.GetForReviewAsync(quotationId, SupplierId);
        return quotation == null ? NotFound() : Ok(quotation);
    }

    [HttpPost("{quotationId}/submit")]
    public async Task<IActionResult> Submit(int quotationId, [FromBody] SubmitQuotationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.SubmitAsync(quotationId, SupplierId, dto);

        if (!result.Success)
            return result.Error == "Quotation request not found." ? NotFound() : BadRequest(new { message = result.Error });

        var submitted = result.Quotation!;

        await _audit.LogAsync(SupplierId, "QUOTATION_SUBMITTED",
            $"Quotation #{quotationId} submitted for listing #{submitted.ListingID}.",
            null, submitted.ListingID, null, ip,
            JsonSerializer.Serialize(new { submitted.Status, dto.DailyRateZAR, dto.QuoteValidUntil }));

        await _email.SendQuotationSubmittedEmailAsync(
            result.ContractorEmail, result.ContractorName, quotationId, submitted.ListingTitle);

        await _notifications.CreateAsync(result.ContractorID, "QuotationSubmitted",
            "Quote Received",
            $"You've received a quote for your request on \"{submitted.ListingTitle}\" (quotation #{quotationId}).",
            "Quotation", quotationId, emailUser: false);
        return Ok(new { quotation = result.Quotation });
    }
}

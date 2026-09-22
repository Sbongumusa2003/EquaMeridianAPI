using EquaMeridian.DTOs.Invoices;
using EquaMeridian.DTOs.Quotations;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/contractor/quotations")]
[Authorize(Policy = "ContractorOnly")]
public class ContractorQuotationsController : ControllerBase
{
    private readonly IQuotationRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IInvoiceRepository _invoices;
    private readonly ILeaseAgreementRepository _leaseAgreements;
    private readonly IUserRepository _users;
    private readonly AppDbContext _db;
    private readonly INotificationRepository _notifications;

    public ContractorQuotationsController(
        IQuotationRepository repo, IAuditService audit, IEmailService email,
        IInvoiceRepository invoices, ILeaseAgreementRepository leaseAgreements,
        IUserRepository users, AppDbContext db, INotificationRepository notifications)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _invoices = invoices;
        _leaseAgreements = leaseAgreements;
        _users = users;
        _db = db;
        _notifications = notifications;
    }

    private int ContractorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateQuotationRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var validContacts = new[] { "Email", "Phone", "Both" };
        if (!validContacts.Contains(dto.PreferredContact))
            return BadRequest(new { message = "Preferred Contact Method must be Email, Phone, or Both." });

        var contractor = await _users.GetByIdAsync(ContractorId);
        if (contractor == null || contractor.AccountStatus != "Active")
            return StatusCode(403, new
            {
                message = "Your verification documents have not been approved yet. " +
                           "You can browse listings, but you cannot request a quote or complete a purchase until an admin approves your documents."
            });

        var result = await _repo.CreateRequestAsync(ContractorId, dto);
        if (!result.Success)
            return result.Error == "Listing not found." ? NotFound() : BadRequest(new { message = result.Error });

        var quotation = result.Quotation!;

        await _audit.LogAsync(ContractorId, "QUOTATION_REQUESTED",
            $"Contractor requested a quotation (#{quotation.QuotationID}) for listing #{quotation.ListingID}.",
            null, quotation.ListingID, null, Ip,
            JsonSerializer.Serialize(new { dto.StartDate, dto.EndDate, dto.Quantity, dto.DeliveryAddress }));

        await _email.SendQuotationRequestedEmailAsync(
            result.SupplierEmail, result.SupplierName, quotation.QuotationID, result.ListingTitle);

        await _notifications.CreateAsync(result.SupplierID, "QuotationRequested",
            "New Quote Request",
            $"A contractor requested a quote for \"{result.ListingTitle}\" (quotation #{quotation.QuotationID}).",
            "Quotation", quotation.QuotationID, emailUser: false);
        return Ok(new { quotation });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? listingId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (quotations, total) = await _repo.GetAllForContractorAsync(
            ContractorId, listingId, status, search, from, to, page, pageSize);

        return Ok(new { quotations, totalCount = total, page, pageSize });
    }

    [HttpGet("{quotationId}")]
    public async Task<IActionResult> GetById(int quotationId)
    {
        var detail = await _repo.GetDetailForContractorAsync(quotationId, ContractorId);
        return detail == null ? NotFound() : Ok(detail);
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] string ids)
    {
        var idList = (ids ?? string.Empty).Split(',')
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        if (idList.Count < 2)
            return BadRequest(new { message = "Please select at least 2 quotations to compare (maximum 4)." });
        if (idList.Count > 4)
            return BadRequest(new { message = "You can compare a maximum of 4 quotations at a time. Please deselect some quotations." });

        var quotations = await _repo.CompareAsync(idList, ContractorId);

        if (quotations.Count != idList.Count)
            return NotFound(new { message = "One or more selected quotations could not be found." });

        if (quotations.Any(q => q.Status != "Submitted" && q.Status != "Accepted"))
            return BadRequest(new { message = "Only Quoted or Accepted quotations can be compared. Please adjust your selection." });

        var differentListings = quotations.Select(q => q.ListingID).Distinct().Count() > 1;

        return Ok(new
        {
            quotations,
            warning = differentListings
                ? "Selected quotations are for different machinery listings. Comparison may not be meaningful."
                : null
        });
    }

    [HttpPost("{quotationId}/accept")]
    public async Task<IActionResult> Accept(int quotationId)
    {
        AcceptQuotationResult? result = null;
        GenerateInvoiceResult? invoiceResult = null;
        LeaseAgreement? leaseAgreement = null;

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            result = await _repo.AcceptAsync(quotationId, ContractorId);
            if (!result.Success)
            {
                await transaction.RollbackAsync();
                return;
            }

            var quotation = result.Quotation!;

            await _audit.LogAsync(ContractorId, "QUOTATION_ACCEPTED",
                $"Contractor accepted quotation #{quotationId}, creating booking #{result.BookingID}.",
                null, quotation.ListingID, JsonSerializer.Serialize(new { Status = "Submitted" }), Ip,
                JsonSerializer.Serialize(new { Status = "Accepted", result.BookingID }));

            invoiceResult = await _invoices.GenerateSystemAsync(quotationId);
            if (invoiceResult.Success)
            {
                var invoice = invoiceResult.Invoice!;
                await _audit.LogAsync(ContractorId, "INVOICE_GENERATED",
                    $"Invoice #{invoice.InvoiceNumber} automatically generated for quotation #{quotationId}.",
                    null, quotation.ListingID, null, Ip, JsonSerializer.Serialize(new { invoice.InvoiceNumber }));
            }

            leaseAgreement = await _leaseAgreements.CreateFromQuotationAsync(quotationId, result.BookingID);
            await _audit.LogAsync(ContractorId, "LEASE_AGREEMENT_GENERATED",
                $"Lease agreement {leaseAgreement.AgreementNumber} generated for quotation #{quotationId}.",
                null, quotation.ListingID, null, Ip,
                JsonSerializer.Serialize(new { leaseAgreement.AgreementNumber }));

            await transaction.CommitAsync();
        });

        if (result == null || !result.Success)
            return result?.Error == "Quotation not found." ? NotFound() : BadRequest(new { message = result?.Error });

        var acceptedQuotation = result.Quotation!;

        await _email.SendQuotationAcceptedEmailAsync(
            result.SupplierEmail, result.SupplierName, quotationId, result.BookingID, result.ListingTitle);

        await _notifications.CreateAsync(result.SupplierID, "QuotationAccepted",
            "Quote Accepted",
            $"Your quote for \"{result.ListingTitle}\" was accepted — booking #{result.BookingID} has been created. Sign the lease so the contractor can pay and you can deliver.",
            "Booking", result.BookingID, emailUser: false);
        await _notifications.CreateAsync(result.SupplierID, "LeaseAgreementSignatureRequired",
            "Lease agreement awaiting your signature",
            $"Please sign the lease for \"{result.ListingTitle}\" (booking #{result.BookingID}). Open Lease agreements to sign so fulfilment can start.",
            "Booking", result.BookingID, emailUser: false);
        if (invoiceResult!.Success)
        {
            var invoice = invoiceResult.Invoice!;
            await _email.SendInvoiceGeneratedEmailAsync(
                invoiceResult.ContractorEmail, invoiceResult.ContractorName, invoice.InvoiceID, invoice.InvoiceNumber, invoice.TotalAmount);
            await _email.SendInvoiceGeneratedEmailAsync(
                invoiceResult.SupplierEmail, invoiceResult.SupplierName, invoice.InvoiceID, invoice.InvoiceNumber, invoice.SupplierPayableAmount);

            await _notifications.CreateAsync(invoiceResult.ContractorID, "InvoiceGenerated",
                "Invoice Ready",
                $"Invoice {invoice.InvoiceNumber} (R{invoice.TotalAmount:N2}) is ready for payment.",
                "Invoice", invoice.InvoiceID, emailUser: false);            await _notifications.CreateAsync(invoiceResult.SupplierID, "InvoiceGenerated",
                "Invoice Generated",
                $"Invoice {invoice.InvoiceNumber} has been generated for booking #{result.BookingID}.",
                "Invoice", invoice.InvoiceID, emailUser: false);        }

        return Ok(new
        {
            quotation = acceptedQuotation,
            bookingId = result.BookingID,
            invoiceId = invoiceResult.Success ? invoiceResult.Invoice!.InvoiceID : (int?)null,
            leaseAgreementId = leaseAgreement!.LeaseAgreementID
        });
    }

    [HttpPost("{quotationId}/reject")]
    public async Task<IActionResult> Reject(int quotationId, [FromBody] RejectQuotationDto dto)
    {
        var result = await _repo.RejectAsync(quotationId, ContractorId, dto.Reason);
        if (!result.Success)
            return result.Error == "Quotation not found." ? NotFound() : BadRequest(new { message = result.Error });

        var quotation = result.Quotation!;

        await _audit.LogAsync(ContractorId, "QUOTATION_REJECTED",
            $"Contractor rejected quotation #{quotationId}.",
            null, quotation.ListingID, JsonSerializer.Serialize(new { Status = "Submitted" }), Ip,
            JsonSerializer.Serialize(new { Status = "Rejected", dto.Reason }));

        await _email.SendQuotationRejectedEmailAsync(
            result.SupplierEmail, result.SupplierName, quotationId, result.ListingTitle);

        await _notifications.CreateAsync(result.SupplierID, "QuotationRejected",
            "Quote Rejected",
            $"Your quote for \"{result.ListingTitle}\" (quotation #{quotationId}) was rejected."
            + (string.IsNullOrWhiteSpace(dto.Reason) ? "" : $" Reason: {dto.Reason}"),
            "Quotation", quotationId, emailUser: false);
        return Ok(new { quotation });
    }
}

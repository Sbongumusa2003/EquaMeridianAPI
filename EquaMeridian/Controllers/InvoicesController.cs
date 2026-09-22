using EquaMeridian.DTOs.Invoices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IPaymentSyncService _paymentSync;
    private readonly INotificationRepository _notifications;

    public InvoicesController(
        IInvoiceRepository repo, IAuditService audit, IEmailService email,
        IPaymentSyncService paymentSync, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _paymentSync = paymentSync; _notifications = notifications; }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpPost("quotations/{quotationId}/generate")]
    [Authorize(Roles = "admin,Contractor")]
    public async Task<IActionResult> Generate(int quotationId)
    {
        var result = await _repo.GenerateForQuotationAsync(quotationId, UserId, Role);
        if (!result.Success)
            return result.ErrorCode switch
            {
                "NotFound" => NotFound(new { message = result.Error }),
                "Forbidden" => Forbid(),
                _ => BadRequest(new { message = result.Error })
            };

        var invoice = result.Invoice!;

        var generatorLabel = Role.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Administrator" : "Contractor";
        await _audit.LogAsync(UserId, "INVOICE_GENERATED",
            $"{generatorLabel} manually generated invoice #{invoice.InvoiceNumber} for quotation #{quotationId}.",
            UserId, invoice.ListingID, null, Ip,
            JsonSerializer.Serialize(new { invoice.InvoiceNumber }));

        await _email.SendInvoiceGeneratedEmailAsync(
            result.ContractorEmail, result.ContractorName, invoice.InvoiceID, invoice.InvoiceNumber, invoice.TotalAmount);
        await _email.SendInvoiceGeneratedEmailAsync(
            result.SupplierEmail, result.SupplierName, invoice.InvoiceID, invoice.InvoiceNumber, invoice.SupplierPayableAmount);

        await _notifications.CreateAsync(result.ContractorID, "InvoiceGenerated",
            "Invoice Ready",
            $"Invoice {invoice.InvoiceNumber} (R{invoice.TotalAmount:N2}) is ready for payment.",
            "Invoice", invoice.InvoiceID, emailUser: false);        await _notifications.CreateAsync(result.SupplierID, "InvoiceGenerated",
            "Invoice Generated",
            $"Invoice {invoice.InvoiceNumber} has been generated.",
            "Invoice", invoice.InvoiceID, emailUser: false);
        // Emails/notifications above needed the real commission figures — but the
        // HTTP response goes straight back to whoever called this endpoint, which
        // can be the Contractor themselves. Redact before it leaves the server.
        if (Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase))
        {
            invoice.PlatformFeePercentage = 0;
            invoice.PlatformFeeAmount = 0;
            invoice.SupplierPayableAmount = 0;
        }

        return Ok(new { message = $"Invoice generated successfully. Invoice #{invoice.InvoiceNumber} created.", invoice });
    }

    [HttpGet("{invoiceId}/pdf")]
    public async Task<IActionResult> DownloadPdf(int invoiceId)
    {
        var invoice = await _repo.GetByIdAsync(invoiceId, UserId, Role);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        var bytes = InvoicePdfService.Build(invoice);
        return File(bytes, "application/pdf", $"{invoice.InvoiceNumber}.pdf");
    }

    [HttpGet("{invoiceId}")]
    public async Task<IActionResult> GetById(int invoiceId)
    {
        var invoice = await _repo.GetByIdAsync(invoiceId, UserId, Role);
        if (invoice == null) return NotFound();

        invoice.PaymentStatus = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);

        return Ok(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (invoices, total) = await _repo.GetAllForUserAsync(UserId, Role, page, pageSize);

        // Same self-heal as GetById — this list previously never refreshed a stale "Pending" row,
        // so it could disagree with the invoice detail page right next to it.
        var invoicesList = invoices.ToList();
        foreach (var inv in invoicesList.Where(i => i.PaymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)))
        {
            inv.PaymentStatus = await _paymentSync.SyncIfPendingAsync(inv.InvoiceID, inv.PaymentStatus);
        }

        return Ok(new { invoices = invoicesList, totalCount = total, page, pageSize });
    }
}

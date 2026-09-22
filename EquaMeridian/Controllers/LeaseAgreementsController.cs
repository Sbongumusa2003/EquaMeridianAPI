using EquaMeridian.DTOs.LeaseAgreements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/lease-agreements")]
[Authorize]
public class LeaseAgreementsController : ControllerBase
{
    private readonly ILeaseAgreementRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public LeaseAgreementsController(
        ILeaseAgreementRepository repo, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var (agreements, total) = await _repo.GetAllForUserAsync(UserId, Role, page, pageSize);
        return Ok(new { agreements, totalCount = total, page, pageSize });
    }

    [HttpGet("{leaseAgreementId}")]
    public async Task<IActionResult> GetById(int leaseAgreementId)
    {
        var agreement = await _repo.GetByIdAsync(leaseAgreementId, UserId, Role);
        if (agreement == null)
            return NotFound(new { message = "Agreement not found or you do not have permission to view this agreement." });

        return Ok(agreement);
    }

    /// <summary>
    /// Tax invoice as a real PDF — built with the same PdfReportEngine the admin reports use, so it
    /// carries the EquaMeridian letterhead (logo + wordmark), gold/charcoal styling and a properly
    /// paginated summary table, instead of the browser's own "print this HTML page" output.
    /// </summary>
    [HttpGet("{leaseAgreementId}/invoice-pdf")]
    public async Task<IActionResult> GetInvoicePdf(int leaseAgreementId)
    {
        var agreement = await _repo.GetByIdAsync(leaseAgreementId, UserId, Role);
        if (agreement == null)
            return NotFound(new { message = "Agreement not found or you do not have permission to view this agreement." });

        static string Money(decimal? value) => "R" + (value ?? 0m).ToString("N2", CultureInfo.InvariantCulture);
        var generatedBy = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "EquaMeridian Hub";
        var period = $"{agreement.RentalStartDate:dd MMM yyyy} \u2013 {agreement.RentalEndDate:dd MMM yyyy}";

        var doc = new PdfReportEngine("Tax Invoice", agreement.AgreementNumber, generatedBy);

        doc.AddKpis(
            new PdfKpi("From", agreement.SupplierName),
            new PdfKpi("Bill To", agreement.ContractorName),
            new PdfKpi("Rental Period", period),
            new PdfKpi("Status", agreement.Status));

        var itemLabel = agreement.ListingTitle;
        var itemSubLabel = string.Join(" ", new[] { agreement.Make, agreement.Model }.Where(s => !string.IsNullOrWhiteSpace(s)));

        doc.AddTable("Equipment",
            new[] { "Item", "Qty", "Amount" },
            new[] { 328.0, 60.0, 140.0 },
            new[] { false, true, true },
            new List<PdfTableRow>
            {
                new PdfTableRow(new[]
                {
                    string.IsNullOrWhiteSpace(itemSubLabel) ? itemLabel : $"{itemLabel} ({itemSubLabel})",
                    agreement.Quantity.ToString(CultureInfo.InvariantCulture),
                    Money(agreement.RentalSubtotal)
                })
            });

        var summaryRows = new List<PdfTableRow>
        {
            new PdfTableRow(new[] { "Subtotal", Money(agreement.RentalSubtotal) }),
        };
        if ((agreement.DiscountAmount ?? 0m) > 0m)
            summaryRows.Add(new PdfTableRow(new[] { "Discount", "-" + Money(agreement.DiscountAmount) }));
        summaryRows.Add(new PdfTableRow(new[]
        {
            "Delivery",
            (agreement.DeliveryFee ?? 0m) > 0m ? Money(agreement.DeliveryFee) : "Free"
        }));
        if (agreement.VatAmount.HasValue)
            summaryRows.Add(new PdfTableRow(new[] { "VAT", Money(agreement.VatAmount) }));
        summaryRows.Add(new PdfTableRow(new[] { "Total (incl. VAT)", Money(agreement.TotalAmount) }, PdfRowStyle.GrandTotal));

        doc.AddTable("Summary",
            new[] { "", "" },
            new[] { 388.0, 140.0 },
            new[] { false, true },
            summaryRows);

        doc.AddNote($"Payment due {agreement.PaymentDueDate:dd MMM yyyy} \u00b7 {agreement.PaymentMethod}. " +
            "This is a computer-generated tax invoice and is valid without a signature.");

        var bytes = doc.Build();
        return File(bytes, "application/pdf", $"{agreement.AgreementNumber}.pdf");
    }

    [HttpPost("{leaseAgreementId}/sign")]
    public async Task<IActionResult> Sign(int leaseAgreementId, [FromBody] SignLeaseAgreementDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.SignAsync(leaseAgreementId, UserId, Role, dto);
        if (!result.Success)
            return result.Error!.StartsWith("Agreement not found")
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });

        var agreement = result.Agreement!;

        await _audit.LogAsync(UserId, "LEASE_AGREEMENT_SIGNED",
            $"{result.SignerName} signed lease agreement {agreement.AgreementNumber}.",
            null, null, null, Ip,
            JsonSerializer.Serialize(new { agreement.Status }));

        if (result.FullyExecuted)
        {
            await _email.SendLeaseAgreementFullyExecutedEmailAsync(result.SignerEmail, result.SignerName, agreement.LeaseAgreementID, agreement.AgreementNumber);
            await _email.SendLeaseAgreementFullyExecutedEmailAsync(result.OtherPartyEmail, result.OtherPartyName, agreement.LeaseAgreementID, agreement.AgreementNumber);

            await _notifications.CreateAsync(result.SignerID, "LeaseAgreementExecuted",
                "Lease Agreement Fully Executed",
                $"Lease agreement {agreement.AgreementNumber} is now fully signed and legally binding.",
                "LeaseAgreement", agreement.LeaseAgreementID, emailUser: false); await _notifications.CreateAsync(result.OtherPartyID, "LeaseAgreementExecuted",
                "Lease Agreement Fully Executed",
                $"Lease agreement {agreement.AgreementNumber} is now fully signed and legally binding.",
                "LeaseAgreement", agreement.LeaseAgreementID, emailUser: false);
        }
        else
        {
            await _email.SendLeaseAgreementSignatureRequiredEmailAsync(result.OtherPartyEmail, result.OtherPartyName, agreement.LeaseAgreementID, agreement.AgreementNumber);

            await _notifications.CreateAsync(result.OtherPartyID, "LeaseAgreementSignatureRequired",
                "Your Signature Is Needed",
                $"{result.SignerName} signed lease agreement {agreement.AgreementNumber} — it's now waiting on your signature.",
                "LeaseAgreement", agreement.LeaseAgreementID, emailUser: false);
        }

        var message = result.FullyExecuted
            ? "The lease agreement has been fully executed and is now legally binding."
            : "You have successfully signed the lease agreement. You will be notified when the other party signs.";

        return Ok(new { message, agreement });
    }
}
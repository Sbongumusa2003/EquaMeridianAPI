using EquaMeridian.DTOs.Payments;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentRepository _repo;
    private readonly IPaymentGatewayService _gateway;
    private readonly IPaymentSyncService _paymentSync;
    private readonly IAuditService _audit;
    private readonly ILogger<PaymentsController> _logger;
    private readonly ILeaseAgreementRepository _leaseRepo;
    private readonly IPayoutRepository _payoutRepo;
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly INotificationRepository _notifications;

    public PaymentsController(
        IPaymentRepository repo,
        IPaymentGatewayService gateway,
        IPaymentSyncService paymentSync,
        IAuditService audit,
        ILogger<PaymentsController> logger,
        ILeaseAgreementRepository leaseRepo,
        IPayoutRepository payoutRepo,
        AppDbContext db,
        IConfiguration config,
        INotificationRepository notifications)
    {
        _repo = repo;
        _gateway = gateway;
        _paymentSync = paymentSync;
        _audit = audit;
        _logger = logger;
        _leaseRepo = leaseRepo;
        _payoutRepo = payoutRepo;
        _db = db;
        _config = config;
        _notifications = notifications;
    }

    // Above this ZAR amount, PayFast's card checkout is skipped entirely — the contractor must
    // settle the invoice via EFT instead. Configurable via Payments:GatewayMaxAmount.
    private decimal GatewayMaxAmount =>
        decimal.TryParse(_config["Payments:GatewayMaxAmount"], out var max) ? max : 50000m;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    /// <summary>
    /// Prefer Cloudflare / proxy client IP when present; fall back to connection remote IP.
    /// Required on Render (Cloudflare in front) so PayFast ITN IP checks see the real sender.
    /// </summary>
    private string? Ip
    {
        get
        {
            var cf = Request.Headers["CF-Connecting-IP"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(cf)) return cf.Trim();

            var xff = Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                // Left-most is the original client (PayFast when ITN is posted).
                var first = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(first)) return first;
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    [Authorize(Policy = "ContractorOnly")]
    [HttpPost("invoices/{invoiceId}/initiate")]
    public async Task<IActionResult> Initiate(int invoiceId)
    {
        var invoice = await _repo.GetInvoiceForPaymentAsync(invoiceId, UserId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        if (invoice.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "This invoice has already been paid." });

        if (invoice.PaymentStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
            || invoice.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "This invoice was cancelled with its booking and can no longer be paid." });

        var leaseStatus = await _leaseRepo.GetExecutionStatusForQuotationAsync(invoice.QuotationID);
        if (leaseStatus == null)
        {
            return BadRequest(new
            {
                message = "A lease agreement must be created and signed by both parties before payment can be made."
            });
        }
        if (!leaseStatus.FullyExecuted)
        {
            var waitingOn = !leaseStatus.ContractorSigned ? "your signature" : "the supplier's signature";
            return BadRequest(new
            {
                message = $"Please sign the lease agreement before paying this invoice. It is still waiting on {waitingOn}.",
                leaseAgreementId = leaseStatus.LeaseAgreementID
            });
        }

        if (invoice.TotalAmount > GatewayMaxAmount)
        {
            invoice.PaymentMethod = "EFT";
            await _db.SaveChangesAsync();
            var s = invoice.Supplier;
            return Ok(new
            {
                method = "EFT",
                processUrl = (string?)null,
                fields = new Dictionary<string, string>(),
                gatewayMaxAmount = GatewayMaxAmount,
                amount = invoice.TotalAmount,
                reference = invoice.InvoiceNumber,
                message = "This amount is above the card checkout limit. Pay by EFT using the supplier account details and upload proof of payment.",
                bank = new
                {
                    bankName = s?.BankName,
                    accountName = s?.BankAccountName ?? s?.CompanyName ?? s?.FullName,
                    accountNumber = s?.BankAccountNumber,
                    branchCode = s?.BankBranchCode,
                    accountType = s?.BankAccountType
                },
                bankConfigured = !string.IsNullOrWhiteSpace(s?.BankAccountNumber)
            });
        }

        var nameParts = (invoice.Contractor.FullName ?? string.Empty).Trim().Split(' ', 2);
        var firstName = nameParts.Length > 0 ? nameParts[0].Trim() : "Contractor";
        var lastName = nameParts.Length > 1 ? nameParts[1].Trim() : ".";

        var request = _gateway.CreatePaymentRequest(invoice, firstName, lastName, invoice.Contractor.Email);

        if (string.IsNullOrEmpty(request.ProcessUrl))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Payments are not configured yet. Contact the platform administrator."
            });
        }

        await _repo.SetGatewayReferenceAsync(invoiceId, request.Fields["m_payment_id"]);

        await _audit.LogAsync(UserId, "PAYMENT_INITIATED",
            $"Contractor initiated PayFast checkout for invoice #{invoice.InvoiceNumber}.",
            UserId, invoice.ListingID, null, Ip);

        invoice.PaymentMethod = "PayFast";
        await _db.SaveChangesAsync();
        return Ok(new InitiatePaymentResponseDto { ProcessUrl = request.ProcessUrl, Fields = request.Fields });
    }

    [AllowAnonymous]
    [HttpPost("payfast/notify")]
    public async Task<IActionResult> PayFastNotify()
    {
        Request.EnableBuffering();
        string rawBody;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        var form = await Request.ReadFormAsync();
        var fields = form.ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        if (!_gateway.VerifyItnSignature(rawBody, fields.GetValueOrDefault("signature")))
        {
            _logger.LogWarning("PayFast ITN rejected: signature mismatch. Fields: {@Fields}", fields.Keys);
            return Ok();
        }

        // Behind Cloudflare/Render the apparent client IP is often a proxy edge, not PayFast.
        // Signature + server-side validate are the authoritative checks; treat IP as advisory only.
        var trustedIp = await _gateway.IsTrustedPayFastSourceAsync(Ip);
        if (!trustedIp)
        {
            _logger.LogWarning(
                "PayFast ITN source IP {Ip} is not a recognised PayFast host (common on Render/Cloudflare). Continuing after signature + validate.",
                Ip);
        }

        if (!await _gateway.ConfirmWithPayFastAsync(fields))
        {
            _logger.LogWarning("PayFast ITN rejected: PayFast's own validate endpoint did not confirm it.");
            return Ok();
        }

        if (!fields.TryGetValue("custom_int1", out var invoiceIdStr) || !int.TryParse(invoiceIdStr, out var invoiceId))
        {
            _logger.LogWarning("PayFast ITN accepted but missing/invalid custom_int1 invoice reference.");
            return Ok();
        }

        var invoice = await _repo.GetByIdAsync(invoiceId);
        if (invoice == null)
        {
            _logger.LogWarning("PayFast ITN referenced unknown invoice #{InvoiceId}.", invoiceId);
            return Ok();
        }

        if (fields.TryGetValue("amount_gross", out var grossStr) &&
            decimal.TryParse(grossStr, out var gross) &&
            Math.Abs(gross - invoice.TotalAmount) > 0.01m)
        {
            _logger.LogWarning(
                "PayFast ITN amount mismatch for invoice #{InvoiceId}: expected {Expected}, got {Actual}.",
                invoiceId, invoice.TotalAmount, gross);
            return Ok();
        }

        var payfastStatus = fields.GetValueOrDefault("payment_status", "UNKNOWN");
        var newStatus = payfastStatus switch
        {
            "COMPLETE" => "Paid",
            "FAILED" => "Failed",
            "CANCELLED" => "Cancelled",
            _ => invoice.PaymentStatus
        };

        if (newStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            invoice.PaymentMethod = "PayFast";

        var strategy = _db.Database.CreateExecutionStrategy();
        var previousStatus = invoice.PaymentStatus;
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            await _repo.SaveSyncedStatusAsync(invoice, newStatus);

            if (fields.TryGetValue("pf_payment_id", out var pfPaymentId) && !string.IsNullOrWhiteSpace(pfPaymentId))
                await _repo.SetGatewayReferenceAsync(invoiceId, pfPaymentId);

            await _audit.LogAsync(invoice.ContractorID, "PAYMENT_STATUS_SYNCED",
                $"PayFast ITN updated invoice #{invoice.InvoiceNumber} to '{newStatus}'.",
                null, invoice.ListingID, null, Ip, newStatus);

            await transaction.CommitAsync();
        });

        if (newStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
            !previousStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            await _paymentSync.NotifySupplierOfPaymentAsync(invoice);
        }

        return Ok();
    }

    [Authorize]
    [HttpGet("bookings/{bookingId}/status")]
    public async Task<IActionResult> GetStatus(int bookingId)
    {
        var invoice = await _repo.GetPaymentForBookingAsync(bookingId, UserId);
        if (invoice == null) return NotFound();

        var syncedStatus = await _paymentSync.SyncIfPendingAsync(invoice.InvoiceID, invoice.PaymentStatus);
        var liveStatusUnavailable = invoice.PaymentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            && syncedStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase);

        return Ok(new PaymentStatusDto
        {
            PaymentID = invoice.InvoiceID,
            BookingID = bookingId,
            AmountDue = invoice.TotalAmount,
            AmountPaid = syncedStatus == "Paid" ? invoice.TotalAmount : 0,
            Status = syncedStatus,
            GatewayReference = invoice.GatewayReference,
            LastSyncedDate = invoice.LastSyncedDate,
            LiveStatusUnavailable = liveStatusUnavailable
        });
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] PaymentHistoryQueryDto query)
    {
        var (payments, total) = await _repo.GetHistoryAsync(UserId, query);

        // Bug fix: this list previously only ever showed whatever PaymentStatus last got written by
        // the ITN webhook, so a settled payment could sit on "Pending" here indefinitely even after
        // GetStatus/GetById had long since synced it elsewhere. Self-heal every still-Pending row here
        // too, so Payment History always reflects the true status without the contractor needing to
        // happen to visit the one booking-detail page that used to be the only place this synced.
        var paymentsList = payments.ToList();
        foreach (var payment in paymentsList.Where(p => p.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)))
        {
            payment.Status = await _paymentSync.SyncIfPendingAsync(payment.PaymentID, payment.Status);
        }

        return Ok(new { payments = paymentsList, totalCount = total, query.Page, query.PageSize });
    }

    [Authorize(Policy = "SupplierOnly")]
    [HttpGet("supplier-history")]
    public async Task<IActionResult> GetSupplierHistory(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _payoutRepo.GetHistoryForSupplierAsync(UserId, status, page, pageSize);
        return Ok(new { payments = items, totalCount = total, page, pageSize });
    }

    [Authorize]
    [HttpGet("invoices/{invoiceId}/receipt")]
    public async Task<IActionResult> GetReceipt(int invoiceId)
    {
        // Self-heal a stale "Pending" invoice before gating on it, same as every other payment-status
        // read path — otherwise a genuinely-settled payment whose ITN webhook never arrived would
        // incorrectly report "not yet paid" here even though GetStatus/GetHistory already show Paid.
        var invoice = await _repo.GetByIdAsync(invoiceId);
        if (invoice != null)
            await _paymentSync.SyncIfPendingAsync(invoiceId, invoice.PaymentStatus);

        var receipt = await _repo.GetReceiptDataAsync(invoiceId, UserId);
        if (receipt == null) return NotFound();

        if (!receipt.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "A receipt is only available once payment has been confirmed."
            });
        }

        return Ok(receipt);
    }

    /// <summary>
    /// Admin tool: force a live PayFast status query for a stuck Pending invoice and apply the result.
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost("admin/invoices/{invoiceId}/sync")]
    public async Task<IActionResult> ForceSync(int invoiceId)
    {
        var invoice = await _repo.GetByIdAsync(invoiceId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });

        var previous = invoice.PaymentStatus;
        var synced = await _paymentSync.SyncIfPendingAsync(invoiceId, invoice.PaymentStatus);

        // Re-load after possible status write
        invoice = await _repo.GetByIdAsync(invoiceId) ?? invoice;

        await _audit.LogAsync(UserId, "PAYMENT_FORCE_SYNC",
            $"Admin forced payment sync for invoice #{invoice.InvoiceNumber}: '{previous}' → '{synced}'.",
            UserId, invoice.ListingID, previous, Ip, synced);

        return Ok(new
        {
            invoiceId,
            invoiceNumber = invoice.InvoiceNumber,
            previousStatus = previous,
            status = synced,
            changed = !string.Equals(previous, synced, StringComparison.OrdinalIgnoreCase)
        });
    }


    [Authorize]
    [HttpPost("invoices/{invoiceId}/eft-proof")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> UploadEftProof(int invoiceId, IFormFile file)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        if (invoice.ContractorID != UserId && invoice.SupplierID != UserId)
            return Forbid();
        if (invoice.ContractorID != UserId)
            return StatusCode(403, new { message = "Only the contractor can upload proof of payment." });

        var err = EquaMeridian.Core.Validation.DocumentUploadPolicy.Validate(file);
        if (err != null) return BadRequest(new { message = err });

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "eft");
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(file.FileName);
        var stored = $"inv-{invoiceId}-{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(dir, stored);
        await using (var fs = System.IO.File.Create(full))
            await file.CopyToAsync(fs);

        invoice.EftProofPath = full;
        invoice.EftProofOriginalName = file.FileName;
        invoice.PaymentMethod = "EFT";
        if (!invoice.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            invoice.PaymentStatus = "EftSubmitted";
        await _db.SaveChangesAsync();

        await _audit.LogAsync(UserId, "EFT_PROOF_UPLOADED",
            $"Proof of payment uploaded for invoice #{invoice.InvoiceNumber}.",
            UserId, invoice.ListingID, null, Ip);

        return Ok(new { message = "Proof of payment uploaded. The supplier can now view it.", fileName = file.FileName });
    }

    [Authorize]
    [HttpGet("invoices/{invoiceId}/eft-proof")]
    public async Task<IActionResult> DownloadEftProof(int invoiceId)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "";
        var allowed = invoice.ContractorID == UserId || invoice.SupplierID == UserId
                      || role.Equals("admin", StringComparison.OrdinalIgnoreCase);
        if (!allowed) return Forbid();
        if (string.IsNullOrWhiteSpace(invoice.EftProofPath) || !System.IO.File.Exists(invoice.EftProofPath))
            return NotFound(new { message = "No proof of payment has been uploaded yet." });
        var name = invoice.EftProofOriginalName ?? "proof-of-payment.pdf";
        return PhysicalFile(invoice.EftProofPath, "application/octet-stream", name);
    }

    [Authorize(Policy = "SupplierOnly")]
    [HttpPost("invoices/{invoiceId}/eft-received")]
    public async Task<IActionResult> ConfirmEftReceived(int invoiceId)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceID == invoiceId);
        if (invoice == null) return NotFound(new { message = "Invoice not found." });
        if (invoice.SupplierID != UserId)
            return StatusCode(403, new { message = "Only the supplier on this invoice can confirm the EFT." });
        if (string.IsNullOrWhiteSpace(invoice.EftProofPath))
            return BadRequest(new { message = "Wait until the contractor has uploaded proof of payment." });

        invoice.PaymentMethod = "EFT";
        invoice.PaymentStatus = "Paid";
        await _db.SaveChangesAsync();

        await _paymentSync.NotifySupplierOfPaymentAsync(invoice);

        await _audit.LogAsync(UserId, "EFT_RECEIVED",
            $"Supplier confirmed EFT received for invoice #{invoice.InvoiceNumber}.",
            UserId, invoice.ListingID, null, Ip);

        return Ok(new { message = "Invoice marked as paid (EFT).", paymentStatus = invoice.PaymentStatus });
    }
}

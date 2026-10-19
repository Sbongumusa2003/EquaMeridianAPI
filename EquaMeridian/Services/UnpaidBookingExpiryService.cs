using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// After both parties sign the lease, the contractor must pay within FeeConfiguration.PaymentWindowHours.
/// If still unpaid when PaymentDueDate passes:
///   1. Cancel the booking (non-payment)
///   2. Return units to UnitsAvailable
///   3. Convert the hire invoice into a non-payment fee invoice (platform revenue + VAT)
///   4. Notify contractor and supplier
/// </summary>
public class UnpaidBookingExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<UnpaidBookingExpiryService> _logger;

    public UnpaidBookingExpiryService(IServiceScopeFactory scopes, ILogger<UnpaidBookingExpiryService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessExpiredAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "UnpaidBookingExpiry failed"); }

            try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task ProcessExpiredAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var now = AppTime.Now;

        var feeConfig = await db.FeeConfigurations
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync(ct);

        var feePercent = feeConfig?.NonPaymentFeePercent > 0 ? feeConfig.NonPaymentFeePercent / 100m : 0.25m;
        var feeMin = feeConfig?.NonPaymentFeeMinZAR > 0 ? feeConfig.NonPaymentFeeMinZAR : 500m;
        var feeMax = feeConfig?.NonPaymentFeeMaxZAR > 0 ? feeConfig.NonPaymentFeeMaxZAR : 5000m;
        if (feeMax < feeMin) feeMax = feeMin;
        var vatRate = feeConfig?.VATRate > 0 ? feeConfig.VATRate : 15m;

        var overdue = await db.Bookings
            .Include(b => b.Listing)
            .Include(b => b.Contractor)
            .Include(b => b.Supplier)
            .Where(b => b.Status == "AwaitingPayment")
            .Join(
                db.LeaseAgreements.Where(a => a.Status == "Active" && a.PaymentDueDate < now),
                b => b.BookingID,
                a => a.BookingID,
                (b, a) => new { Booking = b, Lease = a })
            .ToListAsync(ct);

        if (overdue.Count == 0) return;

        foreach (var row in overdue)
        {
            var booking = row.Booking;
            var lease = row.Lease;

            var invoice = await db.Invoices
                .FirstOrDefaultAsync(i => i.QuotationID == lease.QuotationID, ct);

            if (invoice != null &&
                (invoice.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase)
                 || invoice.PaymentStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var rentalBase = invoice != null
                ? Math.Max(0m, invoice.Subtotal - invoice.DiscountAmount)
                : Math.Max(0m, lease.TotalAmount);

            var feeExclVat = Math.Round(rentalBase * feePercent, 2);
            if (feeExclVat < feeMin) feeExclVat = feeMin;
            if (feeExclVat > feeMax) feeExclVat = feeMax;
            var feeVat = Math.Round(feeExclVat * (vatRate / 100m), 2);
            var feeTotal = feeExclVat + feeVat;

            booking.Status = "Cancelled";
            booking.CancelledAt = now;
            booking.CancellationReason = "Automatically cancelled: payment not received by the due date.";
            booking.CancellationFeeApplies = true;

            db.BookingStatusHistories.Add(new BookingStatusHistory
            {
                BookingID = booking.BookingID,
                Stage = "Cancelled",
                ChangedByUserID = 0,
                ChangedByRole = "System",
                Notes = $"Non-payment: due by {lease.PaymentDueDate:dd MMM yyyy HH:mm}. " +
                        $"Platform non-payment fee R{feeExclVat:0.00} + VAT R{feeVat:0.00} = R{feeTotal:0.00}. " +
                        "Units returned to availability. Paying this fee does not reinstate the hire.",
                CreatedDate = now
            });

            var qty = Math.Max(1, booking.Quantity);
            if (booking.Listing != null)
                ListingInventory.ReleaseFromHire(booking.Listing, qty);
            else
            {
                var listing = await db.Listings.FirstOrDefaultAsync(l => l.ListingID == booking.ListingID, ct);
                if (listing != null)
                    ListingInventory.ReleaseFromHire(listing, qty);
            }

            if (invoice != null)
            {
                // Platform revenue only — supplier payable is zero.
                invoice.Subtotal = feeExclVat;
                invoice.DiscountAmount = 0;
                invoice.DeliveryFee = 0;
                invoice.VATRate = vatRate;
                invoice.VATAmount = feeVat;
                invoice.TotalAmount = feeTotal;
                invoice.PlatformFeePercentage = 100m;
                invoice.PlatformFeeAmount = feeExclVat;
                invoice.SupplierPayableAmount = 0;
                invoice.Status = "Active";
                invoice.PaymentStatus = "Pending";
                invoice.DueDate = now.AddDays(7);
            }

            var quotation = await db.Quotations.FirstOrDefaultAsync(q => q.QuotationID == lease.QuotationID, ct);
            if (quotation != null && quotation.Status != "Cancelled" && quotation.Status != "Rejected")
            {
                quotation.Status = "Cancelled";
                quotation.RejectedDate = now;
                quotation.RejectionReason = "Booking cancelled automatically due to non-payment within the payment window.";
            }

            await db.SaveChangesAsync(ct);

            var machinery = booking.Listing?.ListingTitle ?? "machinery";
            try
            {
                await notifications.CreateAsync(
                    booking.ContractorID,
                    "NonPaymentFee",
                    "Booking cancelled — payment not received",
                    $"Your hire of '{machinery}' (booking #{booking.BookingID}) was cancelled because payment was not received by {lease.PaymentDueDate:dd MMM yyyy HH:mm}. " +
                    $"A non-payment fee of R{feeTotal:0.00} (incl. VAT) is now due. Paying this fee does not reinstate the cancelled hire.",
                    "Booking", booking.BookingID, emailUser: true);

                await notifications.CreateAsync(
                    booking.SupplierID,
                    "BookingCancelled",
                    "Hire cancelled — contractor did not pay",
                    $"Booking #{booking.BookingID} for '{machinery}' was cancelled automatically after the payment window expired. " +
                    "The unit has been returned to your available stock.",
                    "Booking", booking.BookingID, emailUser: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Non-payment notifications failed for booking {Id}", booking.BookingID);
            }

            _logger.LogInformation(
                "Non-payment cancel: Booking {BookingId}, fee excl R{Fee} VAT R{Vat} total R{Total}",
                booking.BookingID, feeExclVat, feeVat, feeTotal);
        }
    }
}

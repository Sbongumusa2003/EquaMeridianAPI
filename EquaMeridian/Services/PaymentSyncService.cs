using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class PaymentSyncService : IPaymentSyncService
{
    private readonly AppDbContext _db;
    private readonly IPaymentGatewayService _gateway;
    private readonly IPaymentRepository _payments;
    private readonly IAuditService _audit;
    private readonly INotificationRepository _notifications;
    private readonly IEmailService _email;

    public PaymentSyncService(
        AppDbContext db, IPaymentGatewayService gateway, IPaymentRepository payments,
        IAuditService audit, INotificationRepository notifications, IEmailService email)
    {
        _db = db;
        _gateway = gateway;
        _payments = payments;
        _audit = audit;
        _notifications = notifications;
        _email = email;
    }

    public async Task<string> SyncIfPendingAsync(int invoiceId, string currentStatus)
    {
        if (!currentStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            return currentStatus;

        var invoice = await _payments.GetByIdAsync(invoiceId);
        if (invoice == null || string.IsNullOrWhiteSpace(invoice.GatewayReference))
            return currentStatus;

        var sync = await _gateway.GetTransactionStatusAsync(invoice.GatewayReference);
        if (!sync.Success || sync.Status == null || sync.Status == invoice.PaymentStatus)
            return currentStatus;

        var previousStatus = invoice.PaymentStatus;
        await _payments.SaveSyncedStatusAsync(invoice, sync.Status);

        await _audit.LogAsync(invoice.ContractorID, "PAYMENT_STATUS_SYNCED",
            $"Live PayFast query updated invoice #{invoice.InvoiceNumber} to '{sync.Status}'.",
            null, invoice.ListingID, null, null, sync.Status);

        if (sync.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase) &&
            !previousStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
        {
            await NotifySupplierOfPaymentAsync(invoice);
        }

        return sync.Status;
    }

    public async Task NotifySupplierOfPaymentAsync(Invoice invoice)
    {
        int? bookingId = invoice.Quotation?.BookingID;
        if (!bookingId.HasValue && invoice.QuotationID > 0)
        {
            bookingId = await _db.Quotations.AsNoTracking()
                .Where(q => q.QuotationID == invoice.QuotationID)
                .Select(q => q.BookingID)
                .FirstOrDefaultAsync();
        }

        Booking? booking = null;
        if (bookingId.HasValue && bookingId.Value > 0)
        {
            booking = await _db.Bookings
                .Include(b => b.Supplier)
                .Include(b => b.Contractor)
                .Include(b => b.Listing)
                .Include(b => b.Delivery)
                .FirstOrDefaultAsync(b => b.BookingID == bookingId.Value);
        }

        if (booking == null)
        {
            booking = await _db.Bookings
                .Include(b => b.Supplier)
                .Include(b => b.Contractor)
                .Include(b => b.Listing)
                .Include(b => b.Delivery)
                .Where(b => b.ContractorID == invoice.ContractorID
                            && b.ListingID == invoice.ListingID
                            && b.Status != "Cancelled"
                            && (b.Status == "AwaitingPayment" || b.Status == "AwaitingSignature" || b.Status == "Confirmed"))
                .OrderByDescending(b => b.CreatedDate)
                .FirstOrDefaultAsync();
        }

        if (booking == null) return;
        if (booking.Status == "Cancelled") return;

        if (booking.Status == "AwaitingPayment" || booking.Status == "AwaitingSignature")
        {
            if (booking.Status == "AwaitingPayment" || invoice.PaymentStatus.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                booking.Status = "Confirmed";
        }

        if (booking.Delivery == null)
        {
            booking.Delivery = new Delivery
            {
                BookingID = booking.BookingID,
                Status = "Pending",
                Method = string.IsNullOrWhiteSpace(booking.FulfillmentMethod)
                    ? DeliveryMethods.SupplierDelivery
                    : booking.FulfillmentMethod,
                CreatedDate = AppTime.Now
            };
            _db.Set<Delivery>().Add(booking.Delivery);
        }

        _db.BookingStatusHistories.Add(new BookingStatusHistory
        {
            BookingID = booking.BookingID,
            Stage = booking.Status,
            ChangedByUserID = invoice.ContractorID,
            ChangedByRole = "System",
            Notes = $"Payment received for invoice #{invoice.InvoiceNumber} — Supplier notified to arrange delivery/pickup.",
            CreatedDate = AppTime.Now
        });
        await _db.SaveChangesAsync();

        await _notifications.CreateAsync(booking.SupplierID, "PaymentReceived",
            "Payment Received",
            $"Payment for booking #{booking.BookingID} (invoice #{invoice.InvoiceNumber}) has been received. " +
            "You can now arrange delivery or hand-over for pickup.",
            "Booking", booking.BookingID, emailUser: false);

        await _email.SendPaymentReceivedEmailAsync(
            booking.Supplier.Email, booking.Supplier.FullName, booking.BookingID, invoice.InvoiceNumber, invoice.SupplierPayableAmount);

        if (booking.Contractor != null && !string.IsNullOrWhiteSpace(booking.Contractor.Email))
        {
            var title = booking.Listing?.ListingTitle ?? "your booking";
            await _notifications.CreateAsync(booking.ContractorID, "PaymentConfirmed",
                "Payment confirmed",
                $"Your payment for booking #{booking.BookingID} (invoice #{invoice.InvoiceNumber}) was received. " +
                $"The supplier will arrange delivery or pickup for \"{title}\".",
                "Booking", booking.BookingID, emailUser: false);

            await _email.SendNotificationEmailAsync(
                booking.Contractor.Email,
                booking.Contractor.FullName,
                "Payment confirmed",
                $"Hi {booking.Contractor.FullName},<br><br>"
                + $"Your payment for booking <strong>#{booking.BookingID}</strong> "
                + $"(invoice <strong>#{invoice.InvoiceNumber}</strong>) has been confirmed.<br><br>"
                + $"Machinery: <strong>{title}</strong><br>"
                + "The supplier has been notified and will arrange delivery or pickup.<br><br>"
                + "Log in to track your booking.",
                "PaymentConfirmed");
        }
    }
}
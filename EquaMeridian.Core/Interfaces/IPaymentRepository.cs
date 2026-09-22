using EquaMeridian.DTOs.Payments;

public class ReceiptData
{
    public int PaymentID { get; set; }
    public decimal Amount { get; set; }
    public decimal VATAmount { get; set; }
    public DateTime TransactionDate { get; set; }
    public int BookingID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public interface IPaymentRepository
{
    Task<Invoice?> GetPaymentForBookingAsync(int bookingId, int userId);
    Task SaveSyncedStatusAsync(Invoice invoice, string newStatus);
    Task<(IEnumerable<PaymentHistoryItemDto> Payments, int TotalCount)> GetHistoryAsync(
        int contractorId, PaymentHistoryQueryDto query);
    Task<ReceiptData?> GetReceiptDataAsync(int invoiceId, int contractorId);

    Task<Invoice?> GetInvoiceForPaymentAsync(int invoiceId, int contractorId);

    Task<Invoice?> GetByIdAsync(int invoiceId);

    Task SetGatewayReferenceAsync(int invoiceId, string gatewayReference);
}
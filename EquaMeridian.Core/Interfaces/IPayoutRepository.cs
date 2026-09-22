using EquaMeridian.DTOs.Payouts;

public class PayoutRequestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public PayoutDto? Payout { get; set; }
}

public interface IPayoutRepository
{

    Task<IEnumerable<EligibleInvoiceDto>> GetEligibleInvoicesAsync(int supplierId);
    Task<PayoutRequestResult> CreateRequestAsync(int supplierId, int invoiceId, string? supplierNotes);
    Task<IEnumerable<PayoutDto>> GetAllForSupplierAsync(int supplierId);
    Task<PayoutDto?> GetByIdForSupplierAsync(int payoutId, int supplierId);
    Task<(IEnumerable<SupplierPaymentHistoryItemDto> Items, int TotalCount)> GetHistoryForSupplierAsync(
        int supplierId, string? status, int page, int pageSize);


    Task<(IEnumerable<PayoutDto> Payouts, int TotalCount)> GetAllAsync(
        string? search, string? status, int page, int pageSize);
    Task<PayoutDto?> GetByIdAsync(int payoutId);
    Task<PayoutRequestResult> ProcessAsync(
        int payoutId, string newStatus, string? administratorNotes, string? declineReason, int adminId);
}

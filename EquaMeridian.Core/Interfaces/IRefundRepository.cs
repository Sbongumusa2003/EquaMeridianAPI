using EquaMeridian.DTOs.Refunds;

public interface IRefundRepository
{
    Task<(IEnumerable<RefundDto> Refunds, int TotalCount)> GetAllAsync(
        string? search, string? status, int page, int pageSize);

    Task<RefundDto?> GetByIdAsync(int refundId);

    Task<RefundRequestResult> CreateContractorRequestAsync(int contractorId, int invoiceId, string reason);

    Task<RefundDto?> GetByIdForContractorAsync(int refundId, int contractorId);
    Task<IEnumerable<RefundDto>> GetAllForContractorAsync(int contractorId);

    Task<RefundRequestResult> ProcessAsync(int refundId, string newStatus, string? administratorNotes, string? failureReason);
}

public class RefundRequestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
    public RefundDto? Refund { get; set; }
}
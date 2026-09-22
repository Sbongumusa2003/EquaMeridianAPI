using EquaMeridian.DTOs.Quotations;

public interface IQuotationRepository
{
    Task<(IEnumerable<QuotationListItemDto> Quotations, int TotalCount)> GetAllForSupplierAsync(
        int supplierId, int? listingId, string? status, int page, int pageSize);
    Task<QuotationReviewDto?> GetForReviewAsync(int quotationId, int supplierId);
    Task<QuotationSubmitResult> SubmitAsync(int quotationId, int supplierId, SubmitQuotationDto dto);
    Task<CreateQuotationResult> CreateRequestAsync(int contractorId, CreateQuotationRequestDto dto);
    Task<(IEnumerable<QuotationListItemDto> Quotations, int TotalCount)> GetAllForContractorAsync(
        int contractorId, int? listingId, string? status, string? search,
        DateTime? from, DateTime? to, int page, int pageSize);
    Task<QuotationDetailDto?> GetDetailForContractorAsync(int quotationId, int contractorId);
    Task<List<QuotationCompareDto>> CompareAsync(IEnumerable<int> quotationIds, int contractorId);
    Task<AcceptQuotationResult> AcceptAsync(int quotationId, int contractorId);
    Task<RejectQuotationResult> RejectAsync(int quotationId, int contractorId, string? reason);
}

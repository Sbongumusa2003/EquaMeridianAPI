using EquaMeridian.DTOs.LeaseAgreements;

public interface ILeaseAgreementRepository
{
    Task<LeaseAgreement> CreateFromQuotationAsync(int quotationId, int bookingId);
    Task<LeaseAgreementDetailDto?> GetByIdAsync(int leaseAgreementId, int userId, string role);
    Task<(IEnumerable<LeaseAgreementListItemDto> Agreements, int TotalCount)> GetAllForUserAsync(
        int userId, string role, int page, int pageSize);
    Task<SignLeaseAgreementResult> SignAsync(int leaseAgreementId, int userId, string role, SignLeaseAgreementDto dto);
    Task<LeaseExecutionStatus?> GetExecutionStatusForQuotationAsync(int quotationId);
}

public class LeaseExecutionStatus
{
    public int LeaseAgreementID { get; set; }
    public bool SupplierSigned { get; set; }
    public bool ContractorSigned { get; set; }
    public bool FullyExecuted => SupplierSigned && ContractorSigned;
}

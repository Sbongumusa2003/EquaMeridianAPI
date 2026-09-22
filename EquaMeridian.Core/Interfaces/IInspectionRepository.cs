using EquaMeridian.DTOs.Inspections;

public interface IInspectionRepository
{
    Task<(IEnumerable<InspectionListItemDto> Inspections, int TotalCount)> GetAllAsync(
        string? status, int page, int pageSize);
    Task<InspectionListItemDto?> GetByIdAsync(int inspectionId);
    Task<IEnumerable<MachineryOptionDto>> GetAvailableMachineryAsync();
    Task<InspectionRequestResult> RequestAsync(RequestInspectionDto dto, int adminId);
    Task<(IEnumerable<InspectionListItemDto> Inspections, int TotalCount)> GetAllForSupplierAsync(
        int supplierId, string? status, int page, int pageSize);
    Task<InspectionOutcomeDto?> GetForOutcomeAsync(int inspectionId, int supplierId);
    Task<InspectionOutcomeDto?> GetForOutcomeAsAdminAsync(int inspectionId);
    Task<InspectionConfirmResult> ConfirmOutcomeAsAdminAsync(int inspectionId, int adminId, ConfirmOutcomeDto dto);
    Task<InspectionOutcomeDto?> GetForOutcomeAsContractorAsync(int inspectionId, int contractorId);
    Task<InspectionConfirmResult> ConfirmOutcomeAsContractorAsync(int inspectionId, int contractorId, ConfirmOutcomeDto dto);
    Task<IEnumerable<MachineryOptionDto>> GetActiveMachineryAsync();
    Task<(IEnumerable<InspectionListItemDto> Inspections, int TotalCount)> GetAllForContractorAsync(
        int contractorId, string? status, int page, int pageSize);
}

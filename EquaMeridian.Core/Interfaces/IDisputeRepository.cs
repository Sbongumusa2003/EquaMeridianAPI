using EquaMeridian.DTOs.Disputes;

public interface IDisputeRepository
{
    Task<(IEnumerable<DisputeListItemDto> Disputes, int TotalCount)> GetAllAsync(
        string? search, string? status, int page, int pageSize);
    Task<DisputeDetailDto?> GetForReviewAsync(int disputeId);
    Task<DisputeResolutionResult> ResolveAsync(int disputeId, ResolveDisputeDto dto, int adminId);
    Task<RaiseDisputeResult> RaiseAsync(
        int bookingId, int complainantId, string complainantRole,
        RaiseDisputeDto dto, IReadOnlyList<string> evidencePaths);
    Task<(IEnumerable<DisputeListItemDto> Disputes, int TotalCount)> GetForUserAsync(
        int userId, int page, int pageSize);
    Task<DisputeDetailDto?> GetForPartyAsync(int disputeId, int userId);
}

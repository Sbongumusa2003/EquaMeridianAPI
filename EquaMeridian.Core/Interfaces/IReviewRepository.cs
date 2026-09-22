using EquaMeridian.DTOs.Reviews;
using static EquaMeridian.DTOs.Reviews.ReviewDto;

public interface IReviewRepository
{
    Task<ReviewActionResult> CreateAsync(int contractorId, CreateReviewDto dto);
    Task<ReviewsPageDto> GetForListingAsync(
        int listingId, int page, int pageSize, string? sortBy, int? starFilter);
    Task<IEnumerable<ReviewDto>> GetMineAsync(int contractorId, int editWindowDays);
    Task<ReviewActionResult> UpdateAsync(
        int reviewId, int contractorId, UpdateReviewDto dto, int editWindowDays);
    Task<ReviewActionResult> DeleteAsync(int reviewId, int contractorId);
    Task<AdminReviewsPageDto> GetAllForAdminAsync(
        int page, int pageSize, string? status, int? listingId, int? starFilter, string? search);
    Task<ReviewActionResult> AdminDeleteAsync(int reviewId, int adminId, string reason);
}

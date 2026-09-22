using EquaMeridian.DTOs.Listings;

public interface IListingRepository
{
    Task<(IEnumerable<ListingDto> Listings, int TotalCount)> GetAllAsync(
        string? search, int? category, string? status, int page, int pageSize,
        decimal? minPrice = null, decimal? maxPrice = null, string? location = null,
        int? serviceAreaId = null);
    Task<ListingDto?> GetByIdAsync(int listingId);
    Task<IEnumerable<ListingDto>> GetByIdsAsync(IEnumerable<int> ids);
    Task<(bool Success, string? Error)> UpdateStatusAsync(int listingId, string newStatus);
    Task<(IEnumerable<ListingDto> Listings, int TotalCount)> GetBySupplierAsync(
        int supplierId, string? status, int page, int pageSize);
    Task<int> CreateAsync(CreateListingDto dto, int supplierId);
    Task UpdateAsync(int listingId, UpdateListingDto dto);
    Task<(bool Success, string? Error)> DeactivateAsync(int listingId);
    Task<(bool Success, string? Error, bool HardDeleted)> ArchiveAsync(int listingId);
    Task<(bool Success, string? Error)> ApproveAsync(int listingId, int adminId);
    Task<bool> RejectAsync(int listingId, string reason, int adminId);
    Task<bool> RequestChangesAsync(int listingId, string comments, int adminId);
    Task<EquaMeridian.DTOs.Listings.SupplierStorefrontDto?> GetSupplierStorefrontAsync(
        int supplierId, int page = 1, int pageSize = 12);
}

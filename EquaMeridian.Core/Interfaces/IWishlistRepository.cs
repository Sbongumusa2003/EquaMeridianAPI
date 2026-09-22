using EquaMeridian.DTOs.Wishlist;

public interface IWishlistRepository
{
    Task<IEnumerable<WishlistItemDto>> GetByContractorAsync(int contractorId);
    Task<(bool Success, string? Error, WishlistItemDto? Item)> AddAsync(int contractorId, int listingId);
    Task<bool> RemoveAsync(int contractorId, int wishlistItemId);
    Task<bool> RemoveByListingAsync(int contractorId, int listingId);
    Task<HashSet<int>> GetWishlistedListingIdsAsync(int contractorId);
}

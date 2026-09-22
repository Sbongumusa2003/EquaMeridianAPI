using EquaMeridian.DTOs.Wishlist;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class WishlistRepository : IWishlistRepository
{
    private readonly AppDbContext _db;
    private readonly IListingRepository _listings;

    public WishlistRepository(AppDbContext db, IListingRepository listings)
    {
        _db = db;
        _listings = listings;
    }

    public async Task<IEnumerable<WishlistItemDto>> GetByContractorAsync(int contractorId)
    {
        var rows = await _db.WishlistItems
            .Where(w => w.ContractorID == contractorId)
            .OrderByDescending(w => w.AddedDate)
            .AsNoTracking()
            .Select(w => new { w.WishlistItemID, w.AddedDate, w.ListingID })
            .ToListAsync();

        var result = new List<WishlistItemDto>();
        foreach (var row in rows)
        {
            var listing = await _listings.GetByIdAsync(row.ListingID);
            if (listing == null) continue;

            result.Add(new WishlistItemDto
            {
                WishlistItemID = row.WishlistItemID,
                AddedDate = row.AddedDate,
                Listing = listing
            });
        }

        return result;
    }

    public async Task<(bool Success, string? Error, WishlistItemDto? Item)> AddAsync(int contractorId, int listingId)
    {
        var listing = await _listings.GetByIdAsync(listingId);
        if (listing == null)
            return (false, "Listing not found.", null);

        var alreadyExists = await _db.WishlistItems
            .AnyAsync(w => w.ContractorID == contractorId && w.ListingID == listingId);
        if (alreadyExists)
            return (false, "This listing is already in your wishlist.", null);

        var item = new WishlistItem
        {
            ContractorID = contractorId,
            ListingID = listingId,
            AddedDate = AppTime.Now
        };
        _db.WishlistItems.Add(item);
        await _db.SaveChangesAsync();

        return (true, null, new WishlistItemDto
        {
            WishlistItemID = item.WishlistItemID,
            AddedDate = item.AddedDate,
            Listing = listing
        });
    }

    public async Task<bool> RemoveAsync(int contractorId, int wishlistItemId)
    {
        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.WishlistItemID == wishlistItemId && w.ContractorID == contractorId);
        if (item == null) return false;

        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveByListingAsync(int contractorId, int listingId)
    {
        var item = await _db.WishlistItems
            .FirstOrDefaultAsync(w => w.ListingID == listingId && w.ContractorID == contractorId);
        if (item == null) return false;

        _db.WishlistItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<HashSet<int>> GetWishlistedListingIdsAsync(int contractorId)
    {
        var ids = await _db.WishlistItems
            .Where(w => w.ContractorID == contractorId)
            .Select(w => w.ListingID)
            .ToListAsync();
        return ids.ToHashSet();
    }
}

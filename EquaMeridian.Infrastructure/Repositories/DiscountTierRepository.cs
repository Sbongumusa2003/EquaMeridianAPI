using EquaMeridian.DTOs.Fees;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class DiscountTierRepository : IDiscountTierRepository
{
    private readonly AppDbContext _db;
    public DiscountTierRepository(AppDbContext db) => _db = db;

    public async Task<IEnumerable<DiscountTierDto>> GetAllAsync()
    {
        var tiers = await _db.DiscountTiers
            .Include(t => t.Category)
            .AsNoTracking()
            .OrderBy(t => t.CategoryID)
            .ThenBy(t => t.MinDays)
            .ToListAsync();

        return tiers.Select(MapToDto);
    }

    public async Task<DiscountTierDto> CreateAsync(UpsertDiscountTierDto dto, int adminId)
    {
        var tier = new DiscountTier
        {
            CategoryID = dto.CategoryID,
            MinDays = dto.MinDays,
            MaxDays = dto.MaxDays,
            DiscountPercent = dto.DiscountPercent,
            UpdatedByAdminID = adminId,
            UpdatedAt = AppTime.Now
        };
        _db.DiscountTiers.Add(tier);
        await _db.SaveChangesAsync();

        return await GetDtoAsync(tier.DiscountTierID);
    }

    public async Task<DiscountTierDto?> UpdateAsync(int id, UpsertDiscountTierDto dto, int adminId)
    {
        var tier = await _db.DiscountTiers.FindAsync(id);
        if (tier == null) return null;

        tier.CategoryID = dto.CategoryID;
        tier.MinDays = dto.MinDays;
        tier.MaxDays = dto.MaxDays;
        tier.DiscountPercent = dto.DiscountPercent;
        tier.UpdatedByAdminID = adminId;
        tier.UpdatedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        return await GetDtoAsync(id);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var tier = await _db.DiscountTiers.FindAsync(id);
        if (tier == null) return false;

        _db.DiscountTiers.Remove(tier);
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task<DiscountTierDto> GetDtoAsync(int id)
    {
        var tier = await _db.DiscountTiers.Include(t => t.Category)
            .AsNoTracking().FirstAsync(t => t.DiscountTierID == id);
        return MapToDto(tier);
    }

    private static DiscountTierDto MapToDto(DiscountTier t) => new()
    {
        DiscountTierID = t.DiscountTierID,
        CategoryID = t.CategoryID,
        CategoryName = t.Category?.Name,
        MinDays = t.MinDays,
        MaxDays = t.MaxDays,
        DiscountPercent = t.DiscountPercent,
        UpdatedAt = t.UpdatedAt
    };
}

using EquaMeridian.DTOs.Campaigns;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class CampaignRepository : ICampaignRepository
{
    private readonly AppDbContext _db;
    public CampaignRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<CampaignDto>, int)> GetAllAsync(
        string? search, string? type, string? status, int page, int pageSize)
    {
        var q = _db.Campaigns
            .AsNoTracking()
            .Include(c => c.FeaturedListing)
            .Where(c => c.Status != "Deleted")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(c => c.Name.Contains(search));

        if (!string.IsNullOrWhiteSpace(type))
            q = q.Where(c => c.Type == type);

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(c => c.Status == status);

        var total = await q.CountAsync();
        var campaigns = await q
            .OrderByDescending(c => c.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return (campaigns.Select(MapToDto), total);
    }

    public async Task<CampaignDto?> GetByIdAsync(int campaignId)
    {
        var campaign = await _db.Campaigns
            .AsNoTracking()
            .Include(c => c.FeaturedListing)
            .FirstOrDefaultAsync(c => c.CampaignID == campaignId);

        return campaign == null ? null : MapToDto(campaign);
    }

    public async Task<IEnumerable<CampaignDto>> GetActiveAsync()
    {
        var now = AppTime.Now;
        var campaigns = await _db.Campaigns
            .AsNoTracking()
            .Include(c => c.FeaturedListing)
            .Where(c => c.Status == "Active"
                        && c.DeletedAt == null
                        && c.StartDate <= now
                        && c.EndDate >= now
                        && (c.Audience == "All" || c.Audience == "Contractor" || string.IsNullOrEmpty(c.Audience)))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync();

        return campaigns.Select(MapToDto);
    }

    public async Task<decimal> GetActivePromoDiscountPercentAsync(int listingId)
    {
        var now = AppTime.Now;
        var best = await _db.Campaigns
            .AsNoTracking()
            .Where(c => c.Status == "Active" && c.StartDate <= now && c.EndDate >= now
                     && c.DeletedAt == null
                     && c.FeaturedListingID == listingId
                     && (c.Type == "Discount Code" || c.Type == "Featured Listing")
                     && c.DiscountValue != null)
            .OrderByDescending(c => c.DiscountValue)
            .Select(c => c.DiscountValue!.Value)
            .FirstOrDefaultAsync();

        return best;
    }

    public async Task<PromoCodeResolution> ResolveDiscountCodeAsync(string code, int? listingId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new PromoCodeResolution { Valid = false, Message = "Promo code is required." };

        var now = AppTime.Now;
        var normalized = code.Trim();

        var campaign = await _db.Campaigns.AsNoTracking()
            .Where(c => c.Status == "Active"
                        && c.DeletedAt == null
                        && c.DiscountCode != null
                        && c.DiscountCode.ToLower() == normalized.ToLower()
                        && c.StartDate <= now
                        && c.EndDate >= now
                        && c.DiscountValue != null
                        && c.DiscountValue > 0
                        && (c.Audience == "All" || c.Audience == "Contractor" || string.IsNullOrEmpty(c.Audience)))
            .OrderByDescending(c => c.DiscountValue)
            .FirstOrDefaultAsync();

        if (campaign == null)
            return new PromoCodeResolution { Valid = false, Message = "Invalid or expired promo code." };

        if (listingId.HasValue
            && campaign.FeaturedListingID.HasValue
            && campaign.FeaturedListingID.Value != listingId.Value)
        {
            return new PromoCodeResolution
            {
                Valid = false,
                Message = "This promo code does not apply to this listing.",
                CampaignId = campaign.CampaignID,
                CampaignName = campaign.Name,
                FeaturedListingId = campaign.FeaturedListingID
            };
        }

        return new PromoCodeResolution
        {
            Valid = true,
            Message = "Promo applied.",
            DiscountPercent = campaign.DiscountValue ?? 0,
            CampaignId = campaign.CampaignID,
            CampaignName = campaign.Name,
            FeaturedListingId = campaign.FeaturedListingID
        };
    }

    public async Task<bool> IsDiscountCodeInUseAsync(string discountCode, int? excludeCampaignId = null)
    {
        var q = _db.Campaigns.Where(c =>
            c.DiscountCode != null &&
            c.DiscountCode.ToLower() == discountCode.ToLower() &&
            (c.Status == "Active" || c.Status == "Scheduled"));

        if (excludeCampaignId.HasValue)
            q = q.Where(c => c.CampaignID != excludeCampaignId.Value);

        return await q.AnyAsync();
    }

    public async Task<int> CreateAsync(CreateCampaignDto dto, int adminId)
    {
        var today = AppTime.Now.Date;
        var status = dto.SaveAsDraft
            ? "Draft"
            : dto.StartDate.Date > today ? "Scheduled" : "Active";

        var campaign = new Campaign
        {
            Name = dto.Name,
            Type = dto.Type,
            Description = dto.Description,
            Audience = dto.Audience,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = status,
            BannerImageURL = dto.BannerImageURL,
            DiscountValue = dto.DiscountValue,
            DiscountCode = dto.DiscountCode,
            FeaturedListingID = dto.FeaturedListingID,
            CreatedByAdminID = adminId,
            CreatedDate = AppTime.Now
        };

        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync();
        return campaign.CampaignID;
    }

    public async Task<bool> UpdateAsync(int campaignId, UpdateCampaignDto dto, int adminId)
    {
        var campaign = await _db.Campaigns.FindAsync(campaignId);
        if (campaign == null || campaign.Status == "Deleted") return false;

        campaign.Name = dto.Name;
        campaign.Description = dto.Description;
        campaign.Audience = dto.Audience;
        campaign.StartDate = dto.StartDate;
        campaign.EndDate = dto.EndDate;
        campaign.Status = dto.Status;
        campaign.BannerImageURL = dto.BannerImageURL;
        campaign.DiscountValue = dto.DiscountValue;
        campaign.DiscountCode = dto.DiscountCode;
        campaign.FeaturedListingID = dto.FeaturedListingID;
        campaign.UpdatedByAdminID = adminId;
        campaign.UpdatedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<(bool Success, string? PriorStatus, string? Name)> DeleteAsync(int campaignId, int adminId)
    {
        var campaign = await _db.Campaigns.FindAsync(campaignId);
        if (campaign == null || campaign.Status == "Deleted") return (false, null, null);

        var priorStatus = campaign.Status;
        campaign.Status = "Deleted";
        campaign.DeletedByAdminID = adminId;
        campaign.DeletedAt = AppTime.Now;

        await _db.SaveChangesAsync();
        return (true, priorStatus, campaign.Name);
    }

    private static CampaignDto MapToDto(Campaign c) => new()
    {
        CampaignID = c.CampaignID,
        Name = c.Name,
        Type = c.Type,
        Description = c.Description,
        Audience = c.Audience,
        StartDate = c.StartDate,
        EndDate = c.EndDate,
        Status = c.Status,
        BannerImageURL = c.BannerImageURL,
        DiscountValue = c.DiscountValue,
        DiscountCode = c.DiscountCode,
        FeaturedListingID = c.FeaturedListingID,
        FeaturedListingTitle = c.FeaturedListing?.ListingTitle,
        CreatedDate = c.CreatedDate
    };
}

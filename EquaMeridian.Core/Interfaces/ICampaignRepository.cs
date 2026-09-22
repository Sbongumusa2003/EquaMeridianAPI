using EquaMeridian.DTOs.Campaigns;

public interface ICampaignRepository
{
    Task<(IEnumerable<CampaignDto> Campaigns, int TotalCount)> GetAllAsync(
        string? search, string? type, string? status, int page, int pageSize);
    Task<CampaignDto?> GetByIdAsync(int campaignId);
    Task<bool> IsDiscountCodeInUseAsync(string discountCode, int? excludeCampaignId = null);
    Task<int> CreateAsync(CreateCampaignDto dto, int adminId);
    Task<bool> UpdateAsync(int campaignId, UpdateCampaignDto dto, int adminId);
    Task<(bool Success, string? PriorStatus, string? Name)> DeleteAsync(int campaignId, int adminId);
    Task<IEnumerable<CampaignDto>> GetActiveAsync();
    Task<decimal> GetActivePromoDiscountPercentAsync(int listingId);
    Task<PromoCodeResolution> ResolveDiscountCodeAsync(string code, int? listingId = null);
}

public class PromoCodeResolution
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public int? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public int? FeaturedListingId { get; set; }
}

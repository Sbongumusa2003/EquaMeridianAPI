public class Campaign
{
    public int CampaignID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Audience { get; set; } = "All";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Draft";
    public string? BannerImageURL { get; set; }
    public decimal? DiscountValue { get; set; }
    public string? DiscountCode { get; set; }
    public int? FeaturedListingID { get; set; }
    public Listing? FeaturedListing { get; set; }
    public int CreatedByAdminID { get; set; }
    public User CreatedByAdmin { get; set; } = null!;
    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public int? UpdatedByAdminID { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? DeletedByAdminID { get; set; }
    public DateTime? DeletedAt { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Campaigns
{
    public class CampaignDto
    {
        public int CampaignID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? BannerImageURL { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountCode { get; set; }
        public int? FeaturedListingID { get; set; }
        public string? FeaturedListingTitle { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateCampaignDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [Required]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;
        [Required]
        public string Audience { get; set; } = "All";

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
        public bool SaveAsDraft { get; set; } = false;

        public string? BannerImageURL { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountCode { get; set; }
        public int? FeaturedListingID { get; set; }
    }

    public class UpdateCampaignDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public string Audience { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }
        [Required]
        public string Status { get; set; } = string.Empty;

        public string? BannerImageURL { get; set; }
        public decimal? DiscountValue { get; set; }
        public string? DiscountCode { get; set; }
        public int? FeaturedListingID { get; set; }
    }
}

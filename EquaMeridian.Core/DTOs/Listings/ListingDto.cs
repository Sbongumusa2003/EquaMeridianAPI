namespace EquaMeridian.DTOs.Listings
{
    public class ListingDto
    {
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public int CategoryID { get; set; }
        public string AvailabilityStatus { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? MakeBrand { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? OperatingWeight { get; set; }
        public string? EnginePower { get; set; }
        public string? Location { get; set; }
        public decimal DailyRateZAR { get; set; }
        public decimal? WeeklyRateZAR { get; set; }
        public bool DryHireAvailable { get; set; }
        public bool WetHireAvailable { get; set; }
        public decimal? WetDailyRateZAR { get; set; }
        public decimal? WetWeeklyRateZAR { get; set; }
        public bool PickupAvailable { get; set; }
        public bool DeliveryAvailable { get; set; }
        public decimal? DeliveryFeeZAR { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool DuplicateFlag { get; set; }
        public int SupplierID { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public List<string> ImageUrls { get; set; } = new();
        public List<ListingImageDto> Images { get; set; } = new();
        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string PricingMode { get; set; } = "Fixed";
        public int UnitsOwned { get; set; }
        public int UnitsAvailable { get; set; }
        public int UnitsReserved { get; set; }
        public int UnitsUnderMaintenance { get; set; }
        public bool IsArchived { get; set; }
        public string? AdminReviewNotes { get; set; }
        public DateTime? LastReviewedDate { get; set; }
    }

    public class ListingImageDto
    {
        public int ImageID { get; set; }
        public string Url { get; set; } = string.Empty;
    }
}
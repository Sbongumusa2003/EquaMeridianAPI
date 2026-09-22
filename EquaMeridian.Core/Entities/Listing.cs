public class Listing
{
    public int ListingID { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
    public int CategoryID { get; set; }
    public string AvailabilityStatus { get; set; } = "Active";
    public string Description { get; set; } = string.Empty;
    public string? MakeBrand { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? OperatingWeight { get; set; }
    public string? EnginePower { get; set; }
    public string? Location { get; set; }
    public decimal DailyRateZAR { get; set; }
    public decimal? WeeklyRateZAR { get; set; }
    // The platform commission rate in effect when this listing was created. Locked in at creation
    // so that admin changes to the platform-wide rate only affect new listings going forward,
    // never retroactively changing what existing listings' invoices are commissioned at.
    public decimal? CommissionRateSnapshot { get; set; }
    public bool DryHireAvailable { get; set; } = true;
    public bool WetHireAvailable { get; set; } = false;
    public decimal? WetDailyRateZAR { get; set; }
    public decimal? WetWeeklyRateZAR { get; set; }
    public bool PickupAvailable { get; set; } = true;
    public bool DeliveryAvailable { get; set; } = false;
    public decimal? DeliveryFeeZAR { get; set; }

    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public DateTime? DeactivatedDate { get; set; }
    public bool DuplicateFlag { get; set; } = false;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public decimal AverageRating { get; set; } = 0m;
    public int ReviewCount { get; set; } = 0;
    public string PricingMode { get; set; } = "Fixed";
    public int UnitsOwned { get; set; } = 1;
    public int UnitsAvailable { get; set; } = 1;
    public int UnitsReserved { get; set; } = 0;
    public int UnitsUnderMaintenance { get; set; } = 0;
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedDate { get; set; }
    public string? AdminReviewNotes { get; set; }
    public int? LastReviewedByAdminID { get; set; }
    public DateTime? LastReviewedDate { get; set; }
    public bool MasterLeaseAgreementAccepted { get; set; } = false;
    public DateTime? MasterLeaseAgreementAcceptedDate { get; set; }

}

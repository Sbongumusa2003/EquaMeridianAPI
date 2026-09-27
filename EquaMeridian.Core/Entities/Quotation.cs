public class Quotation
{
    public int QuotationID { get; set; }
    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public DateTime RentalStartDate { get; set; }
    public DateTime RentalEndDate { get; set; }
    public int Quantity { get; set; } = 1;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? SpecialRequirements { get; set; }
    public string PreferredContact { get; set; } = "Email";
    public decimal EstimatedTotal { get; set; }
    public decimal? DailyRateZAR { get; set; }
    public decimal? WeeklyRateZAR { get; set; }
    public decimal? DeliveryFee { get; set; }
    public decimal? DeliveryDistanceKm { get; set; }
    public decimal? RentalSubtotal { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? PriceExclVat { get; set; }
    public decimal? VatRate { get; set; }
    public decimal? PriceInclVat { get; set; }
    public decimal? SecurityDeposit { get; set; }
    public decimal? DamageWaiverFee { get; set; }

    public DateTime? QuoteValidUntil { get; set; }
    public string? NotesToCustomer { get; set; }
    public string Status { get; set; } = "Requested";
    public DateTime RequestedDate { get; set; } = AppTime.Now;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public DateTime? RejectedDate { get; set; }
    public string? RejectionReason { get; set; }
    public int? BookingID { get; set; }
    public Booking? Booking { get; set; }
}

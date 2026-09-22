public class BookingConditionInspection
{
    public int BookingConditionInspectionID { get; set; }
    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public string InspectionType { get; set; } = "Handover";

    public int CompletedByUserID { get; set; }
    public User CompletedByUser { get; set; } = null!;
    public string ChecklistData { get; set; } = string.Empty;
    public string? PhotoUrls { get; set; }
    public string Outcome { get; set; } = "Pass";

    public string? Notes { get; set; }
    public string? DamageDescription { get; set; }
    public decimal? EstimatedRepairCost { get; set; }

    public bool SupplierAcknowledged { get; set; }
    public bool ContractorAcknowledged { get; set; }
    public DateTime CompletedDate { get; set; } = AppTime.Now;
}

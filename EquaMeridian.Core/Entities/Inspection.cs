public class Inspection
{
    public int InspectionID { get; set; }
    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;
    public int RequestedByAdminID { get; set; }
    public User RequestedByAdmin { get; set; } = null!;
    public DateTime ScheduledDate { get; set; }
    public string Status { get; set; } = "Requested";
    public string? Outcome { get; set; }
    public string? Notes { get; set; }
    public DateTime RequestedDate { get; set; } = AppTime.Now;
    public DateTime? OutcomeConfirmedDate { get; set; }
}

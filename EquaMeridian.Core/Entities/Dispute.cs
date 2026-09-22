public class Dispute
{
    public int DisputeID { get; set; }
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public int? ListingID { get; set; }
    public Listing? Listing { get; set; }
    public int? BookingID { get; set; }
    public Booking? Booking { get; set; }
    public string BookingReference { get; set; } = string.Empty;
    public decimal BookingAmount { get; set; }
    public int RaisedByUserID { get; set; }
    public User RaisedBy { get; set; } = null!;
    public int? RespondentID { get; set; }
    public User? Respondent { get; set; }
    public string ReasonCategory { get; set; } = string.Empty;
    public string ComplaintDescription { get; set; } = string.Empty;
    public string? DesiredResolution { get; set; }
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime RaisedDate { get; set; } = AppTime.Now;
    public string? ResolutionType { get; set; }
    public string? ResolutionNotes { get; set; }
    public int? ResolvedByAdminID { get; set; }
    public User? ResolvedByAdmin { get; set; }
    public DateTime? ResolvedDate { get; set; }
}

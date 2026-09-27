public class ReturnRequest
{
    public int ReturnRequestID { get; set; }
    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;
    public DateTime PreferredPickupDate { get; set; }
    public string TimeWindow { get; set; } = string.Empty;
    public string PickupLocation { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool EarlyReturnFeeApplies { get; set; }
    public DateTime RequestedAt { get; set; } = AppTime.Now;
    public string Status { get; set; } = "Open";
}

public class DepositDeduction
{
    public int DepositDeductionID { get; set; }
    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public string DamageDescription { get; set; } = string.Empty;
    public decimal EstimatedRepairCost { get; set; }
    public string PhotoEvidenceUrls { get; set; } = string.Empty;
    public string Status { get; set; } = "PendingReview";
    public DateTime CreatedDate { get; set; } = AppTime.Now;
}

public class Refund
{
    public int RefundID { get; set; }
    public int? DisputeID { get; set; }
    public Dispute? Dispute { get; set; }
    public int? InvoiceID { get; set; }
    public Invoice? Invoice { get; set; }

    public decimal Amount { get; set; }
    public int RequestedByUserID { get; set; }
    public User RequestedBy { get; set; } = null!;
    public string Status { get; set; } = "Pending";

    public string? Reason { get; set; }
    public string? AdministratorNotes { get; set; }
    public string? PaymentGatewayReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public DateTime? DateUpdated { get; set; }
    public DateTime? ProcessedDate { get; set; }
}
public class Payout
{
    public int PayoutID { get; set; }

    public int InvoiceID { get; set; }
    public Invoice Invoice { get; set; } = null!;

    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public decimal GrossAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal PayoutAmount { get; set; }

    public string Status { get; set; } = "Pending";

    public string? SupplierNotes { get; set; }
    public string? AdministratorNotes { get; set; }
    public string? DeclineReason { get; set; }

    public int? ProcessedByAdminID { get; set; }
    public User? ProcessedByAdmin { get; set; }

    public DateTime RequestedDate { get; set; } = AppTime.Now;
    public DateTime? ProcessedDate { get; set; }
}

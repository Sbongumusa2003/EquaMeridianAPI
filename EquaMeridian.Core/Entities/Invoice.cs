public class Invoice
{
    public int InvoiceID { get; set; }
    public int QuotationID { get; set; }
    public Quotation Quotation { get; set; } = null!;
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = AppTime.Now;
    public DateTime DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal DeliveryFee { get; set; } = 0m;
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PlatformFeePercentage { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal SupplierPayableAmount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string Status { get; set; } = "Active";
    public string PaymentStatus { get; set; } = "Pending";
    public int? GeneratedByAdminID { get; set; }
    public User? GeneratedByAdmin { get; set; }
    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public string? GatewayReference { get; set; }
    public DateTime? LastSyncedDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? EftProofPath { get; set; }
    public string? EftProofOriginalName { get; set; }
}


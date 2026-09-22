public class LeaseAgreement
{
    public int LeaseAgreementID { get; set; }
    public string AgreementNumber { get; set; } = string.Empty;
    public int QuotationID { get; set; }
    public Quotation Quotation { get; set; } = null!;
    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public DateTime RentalStartDate { get; set; }
    public DateTime RentalEndDate { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal TotalAmount { get; set; }
    public decimal? DepositAmount { get; set; }
    public DateTime PaymentDueDate { get; set; }
    public string PaymentMethod { get; set; } = "PayFast";
    public string StandardTerms { get; set; } = string.Empty;
    public string? SpecialConditions { get; set; }
    public string CancellationPolicy { get; set; } = string.Empty;
    public string LiabilityAndInsuranceTerms { get; set; } = string.Empty;
    public string DamageAndMaintenanceProvisions { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending_Supplier";
    public bool SupplierSigned { get; set; }
    public string? SupplierSignatureName { get; set; }
    public string? SupplierDigitalSignature { get; set; }
    public DateTime? SupplierSignedDate { get; set; }
    public bool ContractorSigned { get; set; }
    public string? ContractorSignatureName { get; set; }
    public string? ContractorDigitalSignature { get; set; }
    public DateTime? ContractorSignedDate { get; set; }
    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public DateTime? UpdatedDate { get; set; }
    public int? UpdatedByUserID { get; set; }
    public ICollection<JobDocument> Documents { get; set; } = new List<JobDocument>();
}

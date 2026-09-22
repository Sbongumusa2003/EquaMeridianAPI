namespace EquaMeridian.DTOs.Invoices
{
    public class InvoiceDto
    {
        public int InvoiceID { get; set; }
        public int QuotationID { get; set; }
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal VATRate { get; set; }
        public decimal VATAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PlatformFeePercentage { get; set; }
        public decimal PlatformFeeAmount { get; set; }
        public decimal SupplierPayableAmount { get; set; }
        public string Currency { get; set; } = "ZAR";
        public string Status { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string? PaymentMethod { get; set; }
        public bool HasEftProof { get; set; }
        public string? EftProofFileName { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankBranchCode { get; set; }
        public string? BankAccountType { get; set; }
    }

    public class InvoiceListItemDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public DateTime DueDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }
    public class GenerateInvoiceResult
    {
        public bool Success { get; set; }
        public string? ErrorCode { get; set; }
        public string? Error { get; set; }

        public InvoiceDto? Invoice { get; set; }
        public int ContractorID { get; set; }
        public string ContractorEmail { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
    }
}

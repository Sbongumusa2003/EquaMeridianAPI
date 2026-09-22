using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.LeaseAgreements
{
    public class LeaseAgreementDetailDto
    {
        public int LeaseAgreementID { get; set; }
        public string AgreementNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Make { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? Location { get; set; }
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public int Quantity { get; set; }
        public decimal? RentalSubtotal { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? DeliveryFee { get; set; }
        public decimal? PriceExclVat { get; set; }
        public decimal? VatRate { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal? DepositAmount { get; set; }
        public DateTime PaymentDueDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string StandardTerms { get; set; } = string.Empty;
        public string? SpecialConditions { get; set; }
        public string CancellationPolicy { get; set; } = string.Empty;
        public string LiabilityAndInsuranceTerms { get; set; } = string.Empty;
        public string DamageAndMaintenanceProvisions { get; set; } = string.Empty;
        public bool SupplierSigned { get; set; }
        public string? SupplierSignatureName { get; set; }
        public DateTime? SupplierSignedDate { get; set; }
        public bool ContractorSigned { get; set; }
        public string? ContractorSignatureName { get; set; }
        public DateTime? ContractorSignedDate { get; set; }
        public bool CanSign { get; set; }
    }
    public class LeaseAgreementListItemDto
    {
        public int LeaseAgreementID { get; set; }
        public string AgreementNumber { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public bool CanSign { get; set; }
    }
    public class SignLeaseAgreementDto
    {
        [Required]
        public bool AcknowledgeTermsRead { get; set; }

        [Required]
        public bool AcknowledgeLegallyBinding { get; set; }

        [Required]
        [StringLength(150, MinimumLength = 2)]
        public string FullName { get; set; } = string.Empty;

        public DateTime SigningDate { get; set; } = AppTime.Now;
        public string? DigitalSignature { get; set; }
    }
    public class SignLeaseAgreementResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }

        public LeaseAgreementDetailDto? Agreement { get; set; }
        public bool FullyExecuted { get; set; }

        public int OtherPartyID { get; set; }
        public string OtherPartyEmail { get; set; } = string.Empty;
        public string OtherPartyName { get; set; } = string.Empty;
        public int SignerID { get; set; }
        public string SignerEmail { get; set; } = string.Empty;
        public string SignerName { get; set; } = string.Empty;
    }
}

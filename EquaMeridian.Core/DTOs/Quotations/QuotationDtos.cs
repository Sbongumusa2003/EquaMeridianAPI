using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Quotations
{
    public class QuotationListItemDto
    {
        public int QuotationID { get; set; }
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedDate { get; set; }
        public DateTime? QuoteValidUntil { get; set; }

        public string? SupplierName { get; set; }
        public DateTime? RentalStartDate { get; set; }
        public DateTime? RentalEndDate { get; set; }
        public int? Quantity { get; set; }
        public decimal? EstimatedTotal { get; set; }
    }

    public class QuotationReviewDto : QuotationListItemDto
    {
        public string HireType { get; set; } = "Dry";
        public string FulfillmentMethod { get; set; } = "Supplier Delivery";
        public string DeliveryAddress { get; set; } = string.Empty;
        public string? SpecialRequirements { get; set; }

        public decimal? DailyRateZAR { get; set; }
        public decimal? WeeklyRateZAR { get; set; }
        public decimal? DeliveryFee { get; set; }
        public string? NotesToCustomer { get; set; }

        public decimal? DeliveryDistanceKm { get; set; }
        public decimal? RentalSubtotal { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal? PriceExclVat { get; set; }
        public decimal? VatRate { get; set; }
        public decimal? PriceInclVat { get; set; }
        public decimal? SecurityDeposit { get; set; }
        public decimal? DamageWaiverFee { get; set; }
    }

    public class SubmitQuotationDto
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Daily rate must be greater than zero.")]
        public decimal DailyRateZAR { get; set; }

        public decimal? WeeklyRateZAR { get; set; }

        [Required]
        public DateTime QuoteValidUntil { get; set; }

        public string? NotesToCustomer { get; set; }

        [Range(0, 30)]
        public decimal? VatRate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SecurityDeposit { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? DamageWaiverFee { get; set; }
    }
    public class QuotationSubmitResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public QuotationReviewDto? Quotation { get; set; }
        public int ContractorID { get; set; }
        public string ContractorEmail { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
    }
    public class CreateQuotationRequestDto
    {
        [Required]
        public int ListingID { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(1, 10, ErrorMessage = "Quantity must be between 1 and 10.")]
        public int Quantity { get; set; } = 1;

        [Required]
        [StringLength(300, MinimumLength = 5)]
        public string DeliveryAddress { get; set; } = string.Empty;

        [StringLength(500)]
        public string? SpecialRequirements { get; set; }

        [Required]
        public string PreferredContact { get; set; } = "Email";
    }
    public class CreateQuotationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public QuotationListItemDto? Quotation { get; set; }
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
    }
    public class QuotationDetailDto : QuotationReviewDto
    {
        public new string SupplierName { get; set; } = string.Empty;
        public new DateTime RentalStartDate { get; set; }
        public new DateTime RentalEndDate { get; set; }
        public new int Quantity { get; set; }
        public new string DeliveryAddress { get; set; } = string.Empty;
        public new string? SpecialRequirements { get; set; }
        public string PreferredContact { get; set; } = string.Empty;
        public new decimal EstimatedTotal { get; set; }
        public List<QuotationStatusEventDto> StatusHistory { get; set; } = new();

        public bool CanAccept { get; set; }
        public bool CanReject { get; set; }

        public int? InvoiceID { get; set; }
    }

    public class QuotationStatusEventDto
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class QuotationCompareDto
    {
        public int QuotationID { get; set; }
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SupplierCompany { get; set; } = string.Empty;
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public int RentalDurationDays { get; set; }
        public int Quantity { get; set; }
        public decimal? DailyRateZAR { get; set; }
        public decimal EstimatedTotal { get; set; }
        public DateTime? QuoteValidUntil { get; set; }
        public decimal? DepositAmount { get; set; }
        public string? MakeBrand { get; set; }
        public string? Model { get; set; }
        public int? Year { get; set; }
        public string? OperatingWeight { get; set; }
        public string? EnginePower { get; set; }
        public string? Location { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? SpecialRequirements { get; set; }
        public bool CanAccept { get; set; }
    }
    public class RejectQuotationDto
    {
        [StringLength(500)]
        public string? Reason { get; set; }
    }
    public class AcceptQuotationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public QuotationListItemDto? Quotation { get; set; }
        public int BookingID { get; set; }
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
    }

    public class RejectQuotationResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public QuotationListItemDto? Quotation { get; set; }
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ListingTitle { get; set; } = string.Empty;
    }
}

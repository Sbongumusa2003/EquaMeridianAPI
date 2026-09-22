using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Payouts
{
    public class PayoutDto
    {
        public int PayoutID { get; set; }
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int? BookingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;

        public decimal GrossAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal VATAmount { get; set; }
        public decimal PayoutAmount { get; set; }

        public string Status { get; set; } = string.Empty;
        public string? SupplierNotes { get; set; }
        public string? AdministratorNotes { get; set; }
        public string? DeclineReason { get; set; }
        public string? ProcessedByAdminName { get; set; }

        public DateTime RequestedDate { get; set; }
        public DateTime? ProcessedDate { get; set; }
    }

    public class PayoutsPagedResultDto
    {
        public IEnumerable<PayoutDto> Payouts { get; set; } = Array.Empty<PayoutDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class EligibleInvoiceDto
    {
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int? BookingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public decimal VATAmount { get; set; }
        public decimal PayoutAmount { get; set; }
    }

    public class RequestPayoutDto
    {
        [Required(ErrorMessage = "An invoice must be specified.")]
        public int InvoiceID { get; set; }

        public string? SupplierNotes { get; set; }
    }

    public class ProcessPayoutDto
    {
        [Required(ErrorMessage = "A new status is required.")]
        public string NewStatus { get; set; } = string.Empty;

        public string? AdministratorNotes { get; set; }
        public string? DeclineReason { get; set; }
    }
    public class SupplierPaymentHistoryItemDto
    {
        public int PayoutID { get; set; }
        public DateTime Date { get; set; }
        public int? BookingID { get; set; }
        public string BookingReference => BookingID.HasValue ? $"BK-{BookingID}" : "-";
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}

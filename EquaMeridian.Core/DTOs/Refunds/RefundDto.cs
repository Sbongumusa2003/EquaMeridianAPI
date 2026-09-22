namespace EquaMeridian.DTOs.Refunds
{
    public class RefundDto
    {
        public int RefundID { get; set; }
        public int? DisputeID { get; set; }
        public int? InvoiceID { get; set; }
        public int RequestedByUserID { get; set; }
        public string PartyName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? AdministratorNotes { get; set; }
        public string? PaymentGatewayReference { get; set; }
        public string? FailureReason { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTime? ProcessedDate { get; set; }
    }
    public class CreateRefundRequestDto
    {
        public int InvoiceID { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "This field is required.")]
        [System.ComponentModel.DataAnnotations.MinLength(1, ErrorMessage = "This field is required.")]
        public string Reason { get; set; } = string.Empty;
    }

    public class ProcessRefundDto
    {
        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "A new status is required.")]
        public string NewStatus { get; set; } = string.Empty;

        public string? AdministratorNotes { get; set; }
        public string? FailureReason { get; set; }
    }
}
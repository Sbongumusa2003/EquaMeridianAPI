namespace EquaMeridian.DTOs.Payments
{
    public class InitiatePaymentResponseDto
    {
        public string ProcessUrl { get; set; } = string.Empty;
        public Dictionary<string, string> Fields { get; set; } = new();
    }

    public class PaymentStatusDto
    {
        public int PaymentID { get; set; }
        public int BookingID { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? GatewayReference { get; set; }
        public DateTime? LastSyncedDate { get; set; }
        public bool LiveStatusUnavailable { get; set; }
    }
    public class PaymentHistoryItemDto
    {
        public int PaymentID { get; set; }
        public DateTime Date { get; set; }
        public int BookingID { get; set; }
        public string BookingReference => $"BK-{BookingID}";
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class PaymentHistoryQueryDto
    {
        public string? Status { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string Sort { get; set; } = "Newest";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
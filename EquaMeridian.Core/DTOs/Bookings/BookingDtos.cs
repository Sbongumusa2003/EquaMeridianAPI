using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Bookings
{
    public class BookingListItemDto
    {
        public int BookingID { get; set; }
        public int SupplierID { get; set; }
        public int ContractorID { get; set; }
        public string Machinery { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool CanViewAddress { get; set; }
        public bool CanConfirmDelivery { get; set; }
        public bool CanUpdateAddress { get; set; }
        public bool CanMarkReadyForPickup { get; set; }
        public bool CanMarkReadyForReturnPickup { get; set; }
        public bool CanRequestReturn { get; set; }
        public bool CanConfirmReturn { get; set; }
        public bool CanRaiseDispute { get; set; }
        public bool CanLeaveReview { get; set; }
        public bool CanEditReview { get; set; }
        public bool CanDeleteReview { get; set; }
        public int? ReviewID { get; set; }
    }
    public class BookingSummaryCardsDto
    {
        public int ActiveBookings { get; set; }
        public int AwaitingYourAction { get; set; }
        public int CompletedBookings { get; set; }
        public decimal TotalLeasedToDate { get; set; }
    }
    public class BookingsPageDto
    {
        public IEnumerable<BookingListItemDto> Bookings { get; set; } = new List<BookingListItemDto>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public BookingSummaryCardsDto SummaryCards { get; set; } = new();
    }
    public class DeliveryDetailDto
    {
        public int BookingID { get; set; }
        public int SupplierID { get; set; }
        public int ContractorID { get; set; }
        public string Machinery { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = string.Empty;
        public string DeliveryStatus { get; set; } = "Pending";
        public bool HasDelivery { get; set; }
        public string DeliveryMethod { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public bool CanRaiseDispute { get; set; }
    }
    public class UpdateDeliveryAddressDto
    {
        [Required]
        [StringLength(300, MinimumLength = 5)]
        public string DeliveryAddress { get; set; } = string.Empty;
    }
    public class ConfirmDeliveryDto
    {
        public string? DeliveryMethod { get; set; }
        public string? ChecklistData { get; set; }
        public string? Outcome { get; set; }
        public string? Notes { get; set; }

        public string? DamageDescription { get; set; }
        public string? PhotoUrls { get; set; }
    }
    public class ConfirmDeliveryResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public DeliveryDetailDto? Delivery { get; set; }
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
    }
    public class RequestReturnDto
    {
        [Required]
        public string ReturnReason { get; set; } = string.Empty;

        [Required]
        public DateTime PreferredPickupDate { get; set; }

        [Required]
        public string PickupTimeWindow { get; set; } = string.Empty;

        [Required]
        [StringLength(300, MinimumLength = 5)]
        public string PickupLocation { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
    public class ReturnRequestResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int? ReturnRequestID { get; set; }
        public bool EarlyReturnFeeApplies { get; set; }
        public int SupplierID { get; set; }
        public string SupplierEmail { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string Machinery { get; set; } = string.Empty;
    }

    public class ConfirmReturnDto
    {
        [Required]
        public string Condition { get; set; } = string.Empty;

        [Required]
        public string InspectionNotes { get; set; } = string.Empty;

        public string? DamageDescription { get; set; }
        public decimal? EstimatedRepairCost { get; set; }
    }

    public class ConfirmReturnResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int? DepositDeductionID { get; set; }
        public int ContractorID { get; set; }
        public string ContractorEmail { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string Machinery { get; set; } = string.Empty;
        public string Condition { get; set; } = string.Empty;
    }

    public class MarkReadyResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int ContractorID { get; set; }
        public string ContractorEmail { get; set; } = string.Empty;
        public string ContractorName { get; set; } = string.Empty;
        public string Machinery { get; set; } = string.Empty;
    }
    public class TrackingStageDto
    {
        public string Stage { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
        public string? Notes { get; set; }
        public bool IsComplete { get; set; }
        public bool IsCurrent { get; set; }
    }
    public class BookingTrackingDto
    {
        public int BookingID { get; set; }
        public string Machinery { get; set; } = string.Empty;
        public string CurrentStatus { get; set; } = string.Empty;
        public bool IsCancelled { get; set; }
        public List<TrackingStageDto> Stages { get; set; } = new();
    }

    public class CancelBookingDto
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class CancelBookingResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public int OtherPartyUserID { get; set; }
        public string OtherPartyEmail { get; set; } = string.Empty;
        public string OtherPartyName { get; set; } = string.Empty;
        public string CancelledByRole { get; set; } = string.Empty;
        public string Machinery { get; set; } = string.Empty;
        public bool CancellationFeeApplies { get; set; }
        public bool RefundRequestCreated { get; set; }
    }
}

public class Booking
{
    public int BookingID { get; set; }
    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public DateTime RentalStartDate { get; set; }
    public DateTime RentalEndDate { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public string Status { get; set; } = "AwaitingSignature";
    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public string? ConditionOnReturn { get; set; }
    public DateTime? ReturnConfirmedAt { get; set; }

    public DateTime? OffHireDateTime { get; set; }

    public int? HandoverInspectionID { get; set; }
    public BookingConditionInspection? HandoverInspection { get; set; }

    public int? ReturnInspectionID { get; set; }
    public BookingConditionInspection? ReturnInspection { get; set; }
    public int Quantity { get; set; } = 1;
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();

    public Delivery? Delivery { get; set; }
    public ReturnRequest? ReturnRequest { get; set; }
    public DepositDeduction? DepositDeduction { get; set; }
    public DateTime? CancelledAt { get; set; }
    public int? CancelledByUserID { get; set; }
    public string? CancellationReason { get; set; }
    public bool CancellationFeeApplies { get; set; }
}

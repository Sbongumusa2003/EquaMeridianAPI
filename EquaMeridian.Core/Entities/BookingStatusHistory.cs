public class BookingStatusHistory
{
    public int BookingStatusHistoryID { get; set; }

    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Stage { get; set; } = string.Empty;

    public int ChangedByUserID { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public string ChangedByRole { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedDate { get; set; } = AppTime.Now;
}

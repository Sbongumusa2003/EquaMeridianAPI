public class Review
{
    public int ReviewID { get; set; }
    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public int MachineryID { get; set; }
    public Listing Machinery { get; set; } = null!;
    public int SupplierID { get; set; }
    public User Supplier { get; set; } = null!;
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public int OverallRating { get; set; }
    public string? AspectRatingsJson { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ReviewText { get; set; } = string.Empty;
    public string Status { get; set; } = "Published";
    public bool IsEdited { get; set; } = false;
    public DateTime CreatedAt { get; set; } = AppTime.Now;
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

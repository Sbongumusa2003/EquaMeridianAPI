public class Delivery
{
    public int DeliveryID { get; set; }
    public int BookingID { get; set; }
    public Booking Booking { get; set; } = null!;
    public string Status { get; set; } = "Pending";
    public string Method { get; set; } = "Supplier Delivery";
    public DateTime? DeliveryDate { get; set; }
    public DateTime CreatedDate { get; set; } = AppTime.Now;
}

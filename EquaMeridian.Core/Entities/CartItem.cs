public class CartItem
{
    public int CartItemID { get; set; }
    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;
    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public DateTime RentalStartDate { get; set; }
    public DateTime RentalEndDate { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
    public DateTime AddedDate { get; set; } = AppTime.Now;
    /// <summary>When this soft reservation expires the units return to the listing.</summary>
    public DateTime ReservedUntil { get; set; } = AppTime.Now.AddMinutes(30);
}

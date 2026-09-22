public class WishlistItem
{
    public int WishlistItemID { get; set; }

    public int ContractorID { get; set; }
    public User Contractor { get; set; } = null!;

    public int ListingID { get; set; }
    public Listing Listing { get; set; } = null!;

    public DateTime AddedDate { get; set; } = AppTime.Now;
}

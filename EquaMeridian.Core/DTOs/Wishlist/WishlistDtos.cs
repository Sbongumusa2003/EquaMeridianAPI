using System.ComponentModel.DataAnnotations;
using EquaMeridian.DTOs.Listings;

namespace EquaMeridian.DTOs.Wishlist
{
    public class WishlistItemDto
    {
        public int WishlistItemID { get; set; }
        public DateTime AddedDate { get; set; }
        public ListingDto Listing { get; set; } = null!;
    }

    public class AddWishlistItemDto
    {
        [Required]
        public int ListingID { get; set; }
    }
}

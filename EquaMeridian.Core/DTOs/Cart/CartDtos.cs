using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Cart
{
    public class AddCartItemDto
    {
        [Required]
        public int ListingID { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        /// <summary>Optional. "Supplier Delivery" or "Contractor Pickup".</summary>
        public string? FulfillmentMethod { get; set; }

        /// <summary>Required for supplier delivery. For pickup the API fills listing.Location.</summary>
        [StringLength(300)]
        public string? DeliveryAddress { get; set; }
    }

    public class UpdateCartItemDto
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        public string? FulfillmentMethod { get; set; }

        [StringLength(300)]
        public string? DeliveryAddress { get; set; }
    }

    public class CartItemDto
    {
        public int CartItemID { get; set; }
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int UnitsAvailable { get; set; }
        public int Quantity { get; set; }
        public DateTime RentalStartDate { get; set; }
        public DateTime RentalEndDate { get; set; }
        public int RentalDurationDays { get; set; }
        public string DeliveryAddress { get; set; } = string.Empty;
        public string FulfillmentMethod { get; set; } = "Supplier Delivery";
        public decimal DailyRateZAR { get; set; }
        public decimal RentalSubtotal { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        // Breakdown so the UI can describe what kind of discount applied (long-term rental tier
        // vs. a promo/campaign code), instead of just showing one lump discount amount.
        public decimal TierDiscountPercent { get; set; }
        public decimal ItemPromoDiscountPercent { get; set; }
        public decimal DeliveryFee { get; set; }
        public decimal DeliveryDistanceKm { get; set; }
        public decimal PriceExclVat { get; set; }
        public decimal VatRate { get; set; }
        public decimal PriceInclVat { get; set; }
        public bool IsAvailable { get; set; }
    }
    public class CartSupplierGroupDto
    {
        public int SupplierID { get; set; }
        public string SupplierCompany { get; set; } = string.Empty;
        public List<CartItemDto> Items { get; set; } = new();
        public decimal GroupRentalSubtotal { get; set; }
        public decimal GroupDiscountAmount { get; set; }
        public decimal GroupDeliveryFee { get; set; }
        public decimal GroupPriceExclVat { get; set; }
        public decimal GroupVatAmount { get; set; }
        public decimal GroupTotal { get; set; }
    }

    public class CartDto
    {
        public List<CartSupplierGroupDto> SupplierGroups { get; set; } = new();
        public decimal GrandTotal { get; set; }
        public int ItemCount { get; set; }
        public bool HasUnavailableItems { get; set; }
        public string? AppliedPromoCode { get; set; }
        public bool PromoValid { get; set; }
        public string? PromoMessage { get; set; }
        public string? PromoCampaignName { get; set; }
        public decimal PromoDiscountPercent { get; set; }
    }

    public class CheckoutRequestDto
    {
        public string? PromoCode { get; set; }
    }

    public class CheckoutResultBookingDto
    {
        public int BookingID { get; set; }
        public int ListingID { get; set; }
        public string ListingTitle { get; set; } = string.Empty;
        public int InvoiceID { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public int LeaseAgreementID { get; set; }
        public int SupplierID { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class CheckoutResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public List<CheckoutResultBookingDto> Bookings { get; set; } = new();
    }
}

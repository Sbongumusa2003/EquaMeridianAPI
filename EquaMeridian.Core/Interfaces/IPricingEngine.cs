public interface IPricingEngine
{
    Task<PricingResult> CalculateAsync(
        decimal dailyRate,
        int numberOfDays,
        int quantity,
        decimal deliveryDistanceKm,
        int? categoryId = null,
        decimal promoDiscountPercent = 0,
        bool chargeDelivery = true);
    Task<decimal> CalculateDeliveryFeeAsync(decimal distanceKm);
    Task<decimal> GetDiscountPercentAsync(int numberOfDays, int? categoryId = null);
}

public class PricingResult
{
    public decimal RentalSubtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    // Breakdown of DiscountPercent by source, so the UI can describe *why* a discount applied
    // instead of just showing a lump amount.
    public decimal TierDiscountPercent { get; set; }
    public decimal PromoDiscountPercent { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal PriceExclVat { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal PriceInclVat { get; set; }
    public decimal GrandTotal => PriceInclVat;
}

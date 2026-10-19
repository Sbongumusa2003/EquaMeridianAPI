using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
public class PricingEngine : IPricingEngine
{
    private readonly AppDbContext _db;
    public PricingEngine(AppDbContext db) => _db = db;

    public async Task<PricingResult> CalculateAsync(
        decimal dailyRate, int numberOfDays, int quantity,
        decimal deliveryDistanceKm, int? categoryId = null, decimal promoDiscountPercent = 0,
        bool chargeDelivery = true,
        decimal? listingFlatDeliveryFee = null)
    {
        var days = Math.Max(1, numberOfDays);
        var qty = Math.Max(1, quantity);

        var rentalSubtotal = dailyRate * days * qty;

        var tierDiscountPercent = await GetDiscountPercentAsync(days, categoryId);
        var discountPercent = Math.Min(100m, tierDiscountPercent + promoDiscountPercent);
        var discountAmount = Math.Round(rentalSubtotal * (discountPercent / 100m), 2);

        var deliveryFee = 0m;
        if (chargeDelivery)
        {
            // Prefer the supplier's listing flat fee when provided; otherwise platform distance formula.
            // Flat fee is charged once per cart/quote line (not × quantity) — one delivery leg.
            // Distance-based fee still scales with quantity (legacy behaviour).
            if (listingFlatDeliveryFee is > 0)
            {
                deliveryFee = Math.Round(listingFlatDeliveryFee.Value, 2);
            }
            else
            {
                var perUnitDeliveryFee = await CalculateDeliveryFeeAsync(deliveryDistanceKm);
                deliveryFee = Math.Round(perUnitDeliveryFee * qty, 2);
            }
        }

        // VAT is calculated on the rental amount only (after discount).
        // Delivery fee is excluded from the VAT base (zero-rated for tax purposes).
        var taxableAmount = rentalSubtotal - discountAmount;
        var vatRate = await GetVatRateAsync();
        var vatAmount = Math.Round(taxableAmount * (vatRate / 100m), 2);

        // Price excl VAT still includes delivery so totals remain consistent with invoices.
        var priceExclVat = taxableAmount + deliveryFee;
        var priceInclVat = taxableAmount + vatAmount + deliveryFee;

        return new PricingResult
        {
            RentalSubtotal = Math.Round(rentalSubtotal, 2),
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            TierDiscountPercent = tierDiscountPercent,
            PromoDiscountPercent = discountPercent - tierDiscountPercent,
            DeliveryFee = deliveryFee,
            PriceExclVat = Math.Round(priceExclVat, 2),
            VatRate = vatRate,
            VatAmount = vatAmount,
            PriceInclVat = Math.Round(priceInclVat, 2)
        };
    }

    public async Task<decimal> CalculateDeliveryFeeAsync(decimal distanceKm)
    {
        var config = await GetFeeConfigAsync();
        if (distanceKm <= config.DeliveryFreeRadiusKm)
            return config.DeliveryBaseFee;

        var extraKm = distanceKm - config.DeliveryFreeRadiusKm;
        return Math.Round(config.DeliveryBaseFee + (extraKm * config.DeliveryRatePerKm), 2);
    }

    public async Task<decimal> GetDiscountPercentAsync(int numberOfDays, int? categoryId = null)
    {
        var tiers = await _db.DiscountTiers.AsNoTracking().ToListAsync();
        if (tiers.Count == 0) return 0m;

        var match = tiers
            .Where(t => t.CategoryID == categoryId)
            .Where(t => numberOfDays >= t.MinDays && (t.MaxDays == null || numberOfDays <= t.MaxDays))
            .OrderByDescending(t => t.MinDays)
            .FirstOrDefault();

        if (match == null && categoryId != null)
        {
            match = tiers
                .Where(t => t.CategoryID == null)
                .Where(t => numberOfDays >= t.MinDays && (t.MaxDays == null || numberOfDays <= t.MaxDays))
                .OrderByDescending(t => t.MinDays)
                .FirstOrDefault();
        }

        return match?.DiscountPercent ?? 0m;
    }

    private async Task<decimal> GetVatRateAsync()
    {
        var config = await GetFeeConfigAsync();
        return config.VATRate;
    }

    private async Task<FeeConfiguration> GetFeeConfigAsync()
    {
        var config = await _db.FeeConfigurations
            .AsNoTracking()
            .OrderByDescending(f => f.UpdatedAt)
            .FirstOrDefaultAsync();

        return config ?? new FeeConfiguration
        {
            VATRate = 15m,
            DeliveryBaseFee = 350m,
            DeliveryFreeRadiusKm = 50m,
            DeliveryRatePerKm = 15m
        };
    }
}

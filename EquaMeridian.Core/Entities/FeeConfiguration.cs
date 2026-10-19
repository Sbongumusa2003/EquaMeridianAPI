public class FeeConfiguration
{
    public int FeeConfigurationID { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal MinFee { get; set; }
    public decimal MaxFee { get; set; }
    public bool VATInclusive { get; set; }
    public decimal VATRate { get; set; } = 15m;
    public decimal DeliveryBaseFee { get; set; } = 350m;
    public decimal DeliveryFreeRadiusKm { get; set; } = 50m;
    public decimal DeliveryRatePerKm { get; set; } = 15m;

    /// <summary>Hours the contractor has to pay after both parties sign the lease.</summary>
    public int PaymentWindowHours { get; set; } = 48;

    /// <summary>Percent of rental (excl. delivery) charged if payment is not made in time (0–100).</summary>
    public decimal NonPaymentFeePercent { get; set; } = 25m;

    /// <summary>Minimum non-payment fee in ZAR.</summary>
    public decimal NonPaymentFeeMinZAR { get; set; } = 500m;

    /// <summary>Maximum non-payment fee in ZAR.</summary>
    public decimal NonPaymentFeeMaxZAR { get; set; } = 5000m;

    public int? UpdatedByAdminID { get; set; }
    public User? UpdatedByAdmin { get; set; }
    public DateTime UpdatedAt { get; set; } = AppTime.Now;
}

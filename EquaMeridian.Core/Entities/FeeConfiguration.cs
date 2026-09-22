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

    public int? UpdatedByAdminID { get; set; }
    public User? UpdatedByAdmin { get; set; }
    public DateTime UpdatedAt { get; set; } = AppTime.Now;
}

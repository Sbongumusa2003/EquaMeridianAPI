using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Fees
{
    public class FeeConfigurationDto
    {
        public int FeeConfigurationID { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal MinFee { get; set; }
        public decimal MaxFee { get; set; }
        public bool VATInclusive { get; set; }
        public decimal VATRate { get; set; }
        public decimal DeliveryBaseFee { get; set; }
        public decimal DeliveryFreeRadiusKm { get; set; }
        public decimal DeliveryRatePerKm { get; set; }
        public int PaymentWindowHours { get; set; }
        public decimal NonPaymentFeePercent { get; set; }
        public decimal NonPaymentFeeMinZAR { get; set; }
        public decimal NonPaymentFeeMaxZAR { get; set; }
        public string? UpdatedByAdminName { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateFeeConfigurationDto
    {
        [Required]
        [Range(0, 100, ErrorMessage = "Commission rate must be between 0 and 100.")]
        public decimal CommissionRate { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Minimum fee must be a non-negative value.")]
        public decimal MinFee { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Maximum fee must be a non-negative value.")]
        public decimal MaxFee { get; set; }

        public bool VATInclusive { get; set; }

        [Required]
        [Range(0, 30, ErrorMessage = "VAT rate must be between 0 and 30.")]
        public decimal VATRate { get; set; } = 15m;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Delivery base fee must be a non-negative value.")]
        public decimal DeliveryBaseFee { get; set; } = 350m;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Delivery free radius must be a non-negative value.")]
        public decimal DeliveryFreeRadiusKm { get; set; } = 50m;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Delivery rate per km must be a non-negative value.")]
        public decimal DeliveryRatePerKm { get; set; } = 15m;

        [Required]
        [Range(1, 720, ErrorMessage = "Payment window must be between 1 and 720 hours.")]
        public int PaymentWindowHours { get; set; } = 48;

        [Required]
        [Range(0, 100, ErrorMessage = "Non-payment fee percent must be between 0 and 100.")]
        public decimal NonPaymentFeePercent { get; set; } = 25m;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Non-payment fee minimum must be non-negative.")]
        public decimal NonPaymentFeeMinZAR { get; set; } = 500m;

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Non-payment fee maximum must be non-negative.")]
        public decimal NonPaymentFeeMaxZAR { get; set; } = 5000m;
    }
}

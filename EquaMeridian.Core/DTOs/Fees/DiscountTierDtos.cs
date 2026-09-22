using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Fees
{
    public class DiscountTierDto
    {
        public int DiscountTierID { get; set; }
        public int? CategoryID { get; set; }
        public string? CategoryName { get; set; }
        public int MinDays { get; set; }
        public int? MaxDays { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class UpsertDiscountTierDto
    {
        public int? CategoryID { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Minimum days must be at least 1.")]
        public int MinDays { get; set; }
        public int? MaxDays { get; set; }

        [Required]
        [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100.")]
        public decimal DiscountPercent { get; set; }
    }
}

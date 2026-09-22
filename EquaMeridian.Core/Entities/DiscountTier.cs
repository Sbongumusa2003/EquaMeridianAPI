public class DiscountTier
{
    public int DiscountTierID { get; set; }
    public int? CategoryID { get; set; }
    public Category? Category { get; set; }

    public int MinDays { get; set; }
    public int? MaxDays { get; set; }

    public decimal DiscountPercent { get; set; }

    public int? UpdatedByAdminID { get; set; }
    public User? UpdatedByAdmin { get; set; }
    public DateTime UpdatedAt { get; set; } = AppTime.Now;
}

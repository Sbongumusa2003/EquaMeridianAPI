
public class BlockedTerm
{
    public int BlockedTermID { get; set; }
    public string Term { get; set; } = string.Empty;
    public int? AddedByAdminID { get; set; }
    public User? AddedByAdmin { get; set; }
    public DateTime CreatedDate { get; set; } = AppTime.Now;
}

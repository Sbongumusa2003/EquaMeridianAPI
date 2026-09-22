public class ServiceArea
{
    public int ServiceAreaID { get; set; }
    public string Name { get; set; } = string.Empty;
}
public class UserServiceArea
{
    public int UserID { get; set; }
    public User User { get; set; } = null!;
    public int ServiceAreaID { get; set; }
    public ServiceArea ServiceArea { get; set; } = null!;
}

public class Role
{
    public int RoleID { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; } = false;

    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
public class Permission
{
    public int PermissionID { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class RolePermission
{
    public int RoleID { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionID { get; set; }
    public Permission Permission { get; set; } = null!;
}

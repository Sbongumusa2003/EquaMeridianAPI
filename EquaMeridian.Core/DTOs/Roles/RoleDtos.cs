using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Roles
{
    public class PermissionDto
    {
        public int PermissionID { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class RoleDto
    {
        public int RoleID { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsSystemRole { get; set; }
        public List<string> PermissionKeys { get; set; } = new();
        public DateTime CreatedDate { get; set; }
    }

    public class CreateRoleRequest
    {
        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? Description { get; set; }

        public List<string> PermissionKeys { get; set; } = new();
    }

    public class UpdateRolePermissionsRequest
    {
        public List<string> PermissionKeys { get; set; } = new();
    }
}

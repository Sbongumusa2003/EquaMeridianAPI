using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.User
{
    public class UserDto
    {
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string AccountStatus { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastLoginDate { get; set; }
    }

    public class UpdateAccountStatusDto
    {
        [Required]
        public string NewStatus { get; set; } = string.Empty;
        public bool ConfirmOverride { get; set; } = false;
    }

    public class CreateInternalUserDto
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "admin";
    }
    public class UpdateUserRoleDto
    {
        [Required]
        public string Role { get; set; } = "Director";
    }
}
using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.User
{
    public class UpdateProfileDto
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        [MaxLength(100)]
        public string? RegistrationNumber { get; set; }

        public List<int>? ServiceAreaIds { get; set; }

        public string? BankName { get; set; }
        public string? BankAccountName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankBranchCode { get; set; }
        public string? BankAccountType { get; set; }
    }
}
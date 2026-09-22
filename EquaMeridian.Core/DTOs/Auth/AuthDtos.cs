using System.ComponentModel.DataAnnotations;

namespace EquaMeridian.DTOs.Auth
{
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public bool KeepMeSignedIn { get; set; } = false;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int UserID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
        public List<string> Permissions { get; set; } = new();
    }

    public class RegisterRequest
    {
        [Required]
        [MaxLength(200)]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z\s\-\']*$", ErrorMessage = "Name must contain only letters, spaces, hyphens or apostrophes.")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        public string Role { get; set; } = "Supplier";

        [MaxLength(200)]
        [RegularExpression(@"^$|^[a-zA-Z0-9][a-zA-Z0-9\s.\-&',()]*$", ErrorMessage = "Company name contains invalid characters.")]
        public string? CompanyName { get; set; }

        /// <summary>
        /// Valid SA formats only: 0XXXXXXXXX or +27XXXXXXXXX (optional separators stripped server-side).
        /// When provided, must be unique across users.
        /// </summary>
        [RegularExpression(@"^(\+27[0-9]{9}|0[0-9]{9})$",
            ErrorMessage = "Phone number must be a valid South African number with exactly 10 digits (0XXXXXXXXX or +27XXXXXXXXX).")]
        public string? PhoneNumber { get; set; }
    }

    public class RegisterSupplierRequest
    {
        [Required]
        [MaxLength(200)]
        [RegularExpression(@"^[a-zA-Z][a-zA-Z\s\-\']*$", ErrorMessage = "Name must contain only letters, spaces, hyphens or apostrophes.")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9\s.\-&',()]*$", ErrorMessage = "Company name contains invalid characters.")]
        public string CompanyName { get; set; } = string.Empty;

        public string? RegistrationNumber { get; set; }

        /// <summary>
        /// Valid SA formats only: 0XXXXXXXXX or +27XXXXXXXXX.
        /// When provided, must be unique across users.
        /// </summary>
        [RegularExpression(@"^(\+27[0-9]{9}|0[0-9]{9})$",
            ErrorMessage = "Phone number must be a valid South African number with exactly 10 digits (0XXXXXXXXX or +27XXXXXXXXX).")]
        public string? PhoneNumber { get; set; }

        [Required]
        public List<Microsoft.AspNetCore.Http.IFormFile> Documents { get; set; } = new();

        [Required]
        public List<int> DocTypeIds { get; set; } = new();
    }

    public class LoginOutcome
    {
        public bool RequiresOtp { get; set; }
        public string? OtpReference { get; set; }
        public DateTime? OtpExpiresAt { get; set; }
        public LoginResponse? Login { get; set; }
    }

    public class VerifyOtpRequest
    {
        [Required]
        public string OtpReference { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        public string Code { get; set; } = string.Empty;

        public bool KeepMeSignedIn { get; set; } = false;
    }

    public class ResendOtpRequest
    {
        [Required]
        public string OtpReference { get; set; } = string.Empty;
    }

    public class ResendOtpResponse
    {
        public string Message { get; set; } = string.Empty;
        public string OtpReference { get; set; } = string.Empty;
        public DateTime OtpExpiresAt { get; set; }
    }

    public enum ResendOtpStatus { Success, NotFound, TooSoon }

    public class ResendOtpOutcome
    {
        public ResendOtpStatus Status { get; set; }
        public ResendOtpResponse? Response { get; set; }
    }

    public class ToggleTwoFactorRequest
    {
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }

    public class ForgotPasswordResponse
    {
        public string Message { get; set; } = string.Empty;
        public string OtpReference { get; set; } = string.Empty;
        public DateTime OtpExpiresAt { get; set; }
    }

    public class VerifyResetOtpRequest
    {
        [Required]
        public string OtpReference { get; set; } = string.Empty;

        [Required]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits.")]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [Compare("NewPassword")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
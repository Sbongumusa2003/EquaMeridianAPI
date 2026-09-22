public class User
{
    public int UserID { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AccountStatus { get; set; } = "Pending";
    public int FailedAttemptCount { get; set; } = 0;
    public DateTime? LockoutExpiry { get; set; }
    public string? CompanyName { get; set; }
    public string? PhoneNumber { get; set; }

    public string? BankName { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankBranchCode { get; set; }
    public string? BankAccountType { get; set; }
    public bool TwoFactorEnabled { get; set; } = false;

    public string? RegistrationNumber { get; set; }

    public DateTime CreatedDate { get; set; } = AppTime.Now;
    public DateTime? LastLoginDate { get; set; }
    public bool MasterLeaseAgreementSigned { get; set; } = false;
    public DateTime? MasterLeaseAgreementSignedDate { get; set; }
    public string? MasterLeaseAgreementSignatureName { get; set; }

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<UserServiceArea> UserServiceAreas { get; set; } = new List<UserServiceArea>();
}
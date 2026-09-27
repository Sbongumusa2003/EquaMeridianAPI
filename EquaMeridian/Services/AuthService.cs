using EquaMeridian.DTOs.Auth;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IEmailService _email;
    private readonly ISmsService _sms;
    private readonly IAuditService _audit;
    private readonly IDocumentRepository _documents;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly INotificationRepository _notifications;

    public AuthService(AppDbContext db, IConfiguration config,
                       IEmailService email, ISmsService sms, IAuditService audit, IDocumentRepository documents,
                       Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, INotificationRepository notifications)
    { _db = db; _config = config; _email = email; _sms = sms; _audit = audit; _documents = documents; _env = env; _notifications = notifications; }

    public async Task<LoginOutcome?> LoginAsync(LoginRequest dto, string ip)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());
        if (user == null) return null;
        if (user.AccountStatus == "Locked" &&
            user.LockoutExpiry.HasValue &&
            user.LockoutExpiry.Value <= AppTime.Now)
        {
            user.AccountStatus = "Active";
            user.FailedAttemptCount = 0;
            user.LockoutExpiry = null;
            await _db.SaveChangesAsync();
        }

        if (user.AccountStatus != "Active")
            throw new InvalidOperationException(user.AccountStatus);

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            user.FailedAttemptCount++;
            if (user.FailedAttemptCount >= 5)
            {
                user.AccountStatus = "Locked";
                user.LockoutExpiry = AppTime.Now.AddMinutes(30);
                await _email.SendLockoutEmailAsync(user.Email, user.FullName);
                await _audit.LogAsync(user.UserID, "LOGIN_LOCKOUT",
                    "Account locked after 5 failed attempts", null, null, null, ip);
            }
            await _db.SaveChangesAsync();
            return null;
        }

        if (user.TwoFactorEnabled)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var reference = Guid.NewGuid().ToString("N");
            var expiresAt = AppTime.Now.AddMinutes(10);

            _db.OtpCodes.Add(new OtpCode
            {
                Reference = reference,
                UserID = user.UserID,
                CodeHash = HashToken(code),
                ExpiryTimestamp = expiresAt
            });
            await _db.SaveChangesAsync();

            await _email.SendOtpCodeEmailAsync(user.Email, user.FullName, code);
            await _sms.SendSmsAsync(user.PhoneNumber,
                $"Your EquaMeridian verification code is {code}. It expires in 10 minutes.");

            await _audit.LogAsync(user.UserID, "LOGIN_OTP_ISSUED",
                "Password verified; OTP issued for two-factor login", null, null, null, ip);

            return new LoginOutcome { RequiresOtp = true, OtpReference = reference, OtpExpiresAt = expiresAt };
        }

        user.FailedAttemptCount = 0;
        user.LastLoginDate = AppTime.Now;
        await _db.SaveChangesAsync();

        var expiry = dto.KeepMeSignedIn
            ? AppTime.Now.AddDays(_config.GetValue<int>("Jwt:LongExpiryDays"))
            : AppTime.Now.AddMinutes(_config.GetValue<int>("Jwt:ExpiryMinutes"));

        var token = GenerateJwt(user, expiry);
        await _audit.LogAsync(user.UserID, "LOGIN", "Successful login", null, null, null, ip);

        return new LoginOutcome
        {
            RequiresOtp = false,
            Login = new LoginResponse
            {
                Token = token,
                Role = user.Role,
                UserID = user.UserID,
                FullName = user.FullName,
                Expiry = expiry,
                Permissions = await ResolvePermissionsAsync(user.Role)
            }
        };
    }

    public async Task<LoginResponse?> VerifyOtpAsync(VerifyOtpRequest dto, string ip)
    {
        var record = await _db.OtpCodes
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Reference == dto.OtpReference && o.Purpose == "Login"
                && !o.IsUsed && o.ExpiryTimestamp > AppTime.Now);
        if (record == null) return null;

        if (record.AttemptCount >= 5)
        {
            record.IsUsed = true;
            await _db.SaveChangesAsync();
            return null;
        }

        if (record.CodeHash != HashToken(dto.Code))
        {
            record.AttemptCount++;
            await _db.SaveChangesAsync();
            return null;
        }

        record.IsUsed = true;
        var user = record.User;
        user.FailedAttemptCount = 0;
        user.LastLoginDate = AppTime.Now;
        await _db.SaveChangesAsync();

        var expiry = dto.KeepMeSignedIn
            ? AppTime.Now.AddDays(_config.GetValue<int>("Jwt:LongExpiryDays"))
            : AppTime.Now.AddMinutes(_config.GetValue<int>("Jwt:ExpiryMinutes"));
        var token = GenerateJwt(user, expiry);

        await _audit.LogAsync(user.UserID, "LOGIN", "Successful login (OTP verified)", null, null, null, ip);

        return new LoginResponse
        {
            Token = token,
            Role = user.Role,
            UserID = user.UserID,
            FullName = user.FullName,
            Expiry = expiry,
            Permissions = await ResolvePermissionsAsync(user.Role)
        };
    }

    public async Task<ResendOtpOutcome> ResendOtpAsync(ResendOtpRequest dto, string ip)
    {
        const int cooldownSeconds = 30;
        // Not filtered by Purpose — this resend flow is shared between login 2FA and
        // password-reset OTPs (see VerifyOtpAsync/VerifyResetOtpAsync, which DO filter by
        // Purpose so a code can't be replayed across those two flows).
        var record = await _db.OtpCodes
            .Include(o => o.User)
            .FirstOrDefaultAsync(o => o.Reference == dto.OtpReference
                && !o.IsUsed && o.AttemptCount < 5);

        if (record == null)
            return new ResendOtpOutcome { Status = ResendOtpStatus.NotFound };

        var secondsSinceIssued = (AppTime.Now - record.CreatedAt).TotalSeconds;
        if (secondsSinceIssued < cooldownSeconds)
            return new ResendOtpOutcome { Status = ResendOtpStatus.TooSoon };
        record.IsUsed = true;

        var user = record.User;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var reference = Guid.NewGuid().ToString("N");
        var expiresAt = AppTime.Now.AddMinutes(10);

        _db.OtpCodes.Add(new OtpCode
        {
            Reference = reference,
            UserID = user.UserID,
            CodeHash = HashToken(code),
            Purpose = record.Purpose,
            ExpiryTimestamp = expiresAt
        });
        await _db.SaveChangesAsync();

        await _email.SendOtpCodeEmailAsync(user.Email, user.FullName, code);
        if (record.Purpose == "Login")
        {
            await _sms.SendSmsAsync(user.PhoneNumber,
                $"Your EquaMeridian verification code is {code}. It expires in 10 minutes.");
        }

        await _audit.LogAsync(user.UserID,
            record.Purpose == "PasswordReset" ? "PASSWORD_RESET_OTP_RESENT" : "LOGIN_OTP_RESENT",
            record.Purpose == "PasswordReset" ? "OTP resent for password reset" : "OTP resent for two-factor login",
            null, null, null, ip);

        return new ResendOtpOutcome
        {
            Status = ResendOtpStatus.Success,
            Response = new ResendOtpResponse
            {
                Message = "A new code has been sent.",
                OtpReference = reference,
                OtpExpiresAt = expiresAt
            }
        };
    }

    public async Task<(bool Success, string Message)> SetTwoFactorEnabledAsync(int userId, bool enabled, string password)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return (false, "Incorrect password.");

        user.TwoFactorEnabled = enabled;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(userId, enabled ? "TWO_FACTOR_ENABLED" : "TWO_FACTOR_DISABLED",
            $"Two-factor authentication {(enabled ? "enabled" : "disabled")}", null, null, null, null);

        return (true, $"Two-factor authentication {(enabled ? "enabled" : "disabled")}.");
    }

    public async Task LogoutAsync(int userId, string token)
    {
        await _audit.LogAsync(userId, "LOGOUT", "User logged out", null, null, null, null);
    }

    public async Task<ForgotPasswordResponse> ForgotPasswordAsync(string email, string ip)
    {
        // Always return the same message whether or not the account exists, so the response
        // never reveals which emails are registered. Only a real Active account gets a token
        // and an email with a reset link.
        var genericResponse = new ForgotPasswordResponse
        {
            Message = "If that email is registered, a password reset link has been sent to it."
        };

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.Trim().ToLower() && u.AccountStatus == "Active");
        if (user == null) return genericResponse;

        // Rate-limit: max 5 reset emails per hour per user
        var hourAgo = AppTime.Now.AddHours(-1);
        var recentCount = await _db.PasswordReset
            .CountAsync(p => p.UserID == user.UserID && p.CreatedAt >= hourAgo);
        if (recentCount >= 5) return genericResponse;

        // Invalidate any previous unused tokens for this user
        var prior = await _db.PasswordReset
            .Where(p => p.UserID == user.UserID && !p.IsUsed)
            .ToListAsync();
        foreach (var p in prior)
            p.IsUsed = true;

        // Cryptographically random URL-safe token (raw sent in email; only hash stored)
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var expiresAt = AppTime.Now.AddHours(24);

        _db.PasswordReset.Add(new PasswordReset
        {
            UserID = user.UserID,
            TokenHash = HashToken(rawToken),
            ExpiryTimestamp = expiresAt,
            IsUsed = false,
            CreatedAt = AppTime.Now
        });
        await _db.SaveChangesAsync();

        var frontendBase = (_config["App:FrontendBaseUrl"] ?? "https://equameridian-hub.onrender.com").TrimEnd('/');
        var resetUrl = $"{frontendBase}/auth/reset-password?token={Uri.EscapeDataString(rawToken)}";

        await _email.SendPasswordResetEmailAsync(user.Email, user.FullName, resetUrl);

        await _audit.LogAsync(user.UserID, "PASSWORD_RESET_LINK_ISSUED",
            "Password reset link emailed", null, null, null, ip);

        return genericResponse;
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(ResetPasswordRequest dto, string ip)
    {
        if (string.IsNullOrWhiteSpace(dto.Token))
            return (false, "Reset token is missing or invalid.");

        var tokenHash = HashToken(dto.Token.Trim());
        var record = await _db.PasswordReset
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.TokenHash == tokenHash
                && !p.IsUsed
                && p.ExpiryTimestamp > AppTime.Now);

        if (record == null)
            return (false, "This reset link is invalid, expired, or has already been used.");

        record.IsUsed = true;
        var user = record.User;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        // Clear lockout — ownership of the email inbox was just proven.
        if (user.AccountStatus == "Locked")
            user.AccountStatus = "Active";
        user.FailedAttemptCount = 0;
        user.LockoutExpiry = null;
        await _db.SaveChangesAsync();

        await _email.SendPasswordChangedNotificationAsync(user.Email, user.FullName);
        await _audit.LogAsync(user.UserID, "PASSWORD_RESET_COMPLETE",
            "Password reset via email link", null, null, null, ip);

        return (true, "Password reset successfully.");
    }

    public async Task<(bool Success, string Message)> RegisterAsync(RegisterRequest dto, string ip)
    {
        var isContractor = dto.Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase);
        if (!isContractor)
        {
            return (false,
                "Supplier accounts must be registered via the supplier registration form, " +
                "which requires uploading verification documents.");
        }

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());
        if (existing != null)
            return (false, "An account with this email already exists.");

        // Explicit rules (in addition to DTO annotations) so messages are always clear
        if (EquaMeridian.Core.Validation.UserRegistrationValidation.NameContainsDigits(dto.FullName))
            return (false, EquaMeridian.Core.Validation.UserRegistrationValidation.NameNoDigitsErrorMessage);

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            return (false, "Phone number is required.");

        var normalizedPhone = EquaMeridian.Core.Validation.UserRegistrationValidation.NormalizeSaPhone(dto.PhoneNumber);
        if (normalizedPhone == null)
            return (false, EquaMeridian.Core.Validation.UserRegistrationValidation.SaPhoneErrorMessage);

        var phoneCheck = await EnsureUniquePhoneAsync(normalizedPhone);
        if (phoneCheck != null)
            return (false, phoneCheck);

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role = "Contractor",
            CompanyName = dto.CompanyName,
            PhoneNumber = normalizedPhone,
            AccountStatus = "Active",
            CreatedDate = AppTime.Now
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(user.UserID, "USER_REGISTERED",
            $"New Contractor account registered: {dto.Email}", null, null, null, ip);

        return (true, "Account created. You can now log in.");
    }

    public async Task<(bool Success, string Message)> RegisterSupplierAsync(
        RegisterSupplierRequest dto, List<Microsoft.AspNetCore.Http.IFormFile>? documents, string ip)
    {
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.Trim().ToLower());
        if (existing != null)
            return (false, "An account with this email already exists.");

        if (EquaMeridian.Core.Validation.UserRegistrationValidation.NameContainsDigits(dto.FullName))
            return (false, EquaMeridian.Core.Validation.UserRegistrationValidation.NameNoDigitsErrorMessage);

        // Phone optional for supplier, but must be valid SA + unique when provided
        string? normalizedPhoneEarly = null;
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
        {
            normalizedPhoneEarly = EquaMeridian.Core.Validation.UserRegistrationValidation.NormalizeSaPhone(dto.PhoneNumber);
            if (normalizedPhoneEarly == null)
                return (false, EquaMeridian.Core.Validation.UserRegistrationValidation.SaPhoneErrorMessage);

            var phoneCheck = await EnsureUniquePhoneAsync(normalizedPhoneEarly);
            if (phoneCheck != null)
                return (false, phoneCheck);
        }

        if (documents == null || documents.Count == 0)
            return (false, "Please upload at least one verification document (e.g. business registration certificate) to complete supplier registration.");

        if (dto.DocTypeIds == null || dto.DocTypeIds.Count != documents.Count)
            return (false, "Each uploaded document must have a matching document type.");
        var duplicateTypeIds = dto.DocTypeIds
            .GroupBy(id => id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();
        if (duplicateTypeIds.Count > 0)
            return (false, "Each document type can only be submitted once. Please remove the duplicate uploads and try again.");
        foreach (var file in documents)
        {
            var validationError = EquaMeridian.Core.Validation.DocumentUploadPolicy.Validate(file);
            if (validationError != null)
                return (false, validationError);
        }
        var requiredTypes = await _db.DocumentTypes
            .Where(t => t.IsRequired && (t.AppliesToRole == null || t.AppliesToRole == "Supplier"))
            .ToListAsync();
        var missingRequired = requiredTypes
            .Where(t => !dto.DocTypeIds.Contains(t.DocTypeID))
            .Select(t => t.TypeName)
            .ToList();
        if (missingRequired.Count > 0)
            return (false, $"Please upload the following required document(s): {string.Join(", ", missingRequired)}.");
        var writtenFilePaths = new List<string>();
        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            var user = await strategy.ExecuteAsync(async () =>
            {
                writtenFilePaths.Clear();

                await using var transaction = await _db.Database.BeginTransactionAsync();

                var normalizedPhone = EquaMeridian.Core.Validation.UserRegistrationValidation.NormalizeSaPhone(dto.PhoneNumber)
                                      ?? (string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber);
                var newUser = new User
                {
                    FullName = dto.FullName.Trim(),
                    Email = dto.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Role = "Supplier",
                    CompanyName = dto.CompanyName,
                    RegistrationNumber = dto.RegistrationNumber,
                    PhoneNumber = normalizedPhone,
                    AccountStatus = "Pending",
                    CreatedDate = AppTime.Now
                    // Master Lease Agreement is no longer signed at registration — it's now addressed
                    // per-listing, at listing creation, so each lease is tied to that specific listing.
                };
                _db.Users.Add(newUser);
                // Persist the user first so we have a real UserID for the document folder path.
                await _db.SaveChangesAsync();

                // PrepareUploadAsync writes the file to disk and builds the entity WITHOUT
                // calling SaveChanges. A single SaveChanges below commits all documents
                // inside the same transaction — avoids nested SaveChanges inside the
                // EF execution-strategy transaction (previous source of registration failures).
                for (var i = 0; i < documents.Count; i++)
                {
                    var (entity, fullPath) = await _documents.PrepareUploadAsync(
                        newUser.UserID, dto.DocTypeIds[i], documents[i]);
                    _db.Documents.Add(entity);
                    writtenFilePaths.Add(fullPath);
                }
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                return newUser;
            });

            await _audit.LogAsync(user.UserID, "USER_REGISTERED",
                $"New Supplier account registered: {dto.Email} ({documents.Count} document(s) submitted for review)",
                null, null, null, ip);

            var adminEmail = _config["AdminEmail"] ?? "admin@equameridian.co.za";
            await _email.SendNewSupplierPendingReviewAsync(adminEmail, user.UserID, dto.FullName, dto.CompanyName);

            await _notifications.BroadcastAsync(
                "New Supplier Registration Pending Review",
                $"'{dto.FullName}' ({dto.CompanyName}) registered as a Supplier and is awaiting document review before their account can be activated.",
                "Admin", "SupplierRegistrationPendingReview", "User", user.UserID);

            return (true, "Registration submitted. Your documents are now awaiting admin review — you'll be notified by email once your account is approved.");
        }
        catch (Exception ex)
        {
            foreach (var path in writtenFilePaths)
            {
                try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
            }

            Console.WriteLine($"[RegisterSupplierAsync] Failed for {dto.Email}: {ex}");

            return (false, "Something went wrong while saving your documents. Please try again — no account was created.");
        }
    }


    private async Task<List<string>> ResolvePermissionsAsync(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName)) return new List<string>();

        // Built-in admin: every permission in the catalogue
        if (string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return await _db.Permissions
                .AsNoTracking()
                .OrderBy(p => p.PermissionKey)
                .Select(p => p.PermissionKey)
                .ToListAsync();
        }

        // Marketplace roles have no admin permissions
        if (string.Equals(roleName, "Supplier", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleName, "Contractor", StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

        // Custom internal role: only keys assigned under Admin > Roles
        return await _db.RolePermissions
            .AsNoTracking()
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .Where(rp => rp.Role.RoleName.ToLower() == roleName.ToLower())
            .Select(rp => rp.Permission.PermissionKey)
            .Distinct()
            .OrderBy(k => k)
            .ToListAsync();
    }

    private string GenerateJwt(User user, DateTime expiry)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Role,           user.Role),
            new Claim(ClaimTypes.Name,           user.FullName)
        };
        var key = new SymmetricSecurityKey(
                      Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashToken(string raw)
        => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    /// <summary>
    /// Returns an error message if the phone is already registered (comparing normalised SA forms),
    /// or null if the number is free / empty.
    /// </summary>
    private async Task<string?> EnsureUniquePhoneAsync(string? phoneNumber)
    {
        var normalized = EquaMeridian.Core.Validation.UserRegistrationValidation.NormalizeSaPhone(phoneNumber);
        if (normalized == null)
            return null; // empty or invalid — format is enforced by DTO annotations / client validators

        // Compare against both stored forms: local 0… and international +27…
        var localForm = "0" + normalized[3..]; // +27XXXXXXXXX -> 0XXXXXXXXX
        var taken = await _db.Users.AnyAsync(u =>
            u.PhoneNumber != null &&
            (u.PhoneNumber == normalized ||
             u.PhoneNumber == localForm ||
             u.PhoneNumber.Replace(" ", "").Replace("-", "") == normalized ||
             u.PhoneNumber.Replace(" ", "").Replace("-", "") == localForm));

        if (taken)
            return "An account with this phone number already exists.";

        return null;
    }
}
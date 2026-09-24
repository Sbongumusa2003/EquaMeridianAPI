using EquaMeridian.DTOs.User;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/admin/users")]
[Authorize(Policy = "AdminOnly")]
public class UsersController : ControllerBase
{
    private readonly IUserRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IDocumentRepository _documents;
    private readonly AppDbContext _db;

    public UsersController(
        IUserRepository repo,
        IAuditService audit,
        IEmailService email,
        IDocumentRepository documents,
        AppDbContext db)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _documents = documents;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (users, total) = await _repo.GetAllAsync(search, role, status, page, pageSize);

        await _audit.LogAsync(adminId, "ADMIN_PANEL_ACCESS",
            "Admin viewed user list", adminId, null, null, ip);

        return Ok(new { users, totalCount = total, page, pageSize });
    }

    [HttpPatch("{userId}/status")]
    public async Task<IActionResult> UpdateStatus(
        int userId, [FromBody] UpdateAccountStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var user = await _repo.GetByIdAsync(userId);
        if (user == null) return NotFound();

        if (dto.NewStatus == "Active"
            && (user.Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
                || user.Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase))
            && !await _documents.AreRequiredDocumentsApprovedAsync(userId, user.Role))
        {
            return BadRequest(new
            {
                message = "This account cannot be activated because not all required documents have been " +
                           "uploaded and approved yet. Review the user's documents in the Manage panel first."
            });
        }

        // Admin cannot deactivate/disable marketplace users while a booking process is open.
        var deactivating = dto.NewStatus.Equals("Inactive", StringComparison.OrdinalIgnoreCase)
            || dto.NewStatus.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
        if (deactivating
            && (user.Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
                || user.Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase)))
        {
            if (await _repo.HasOpenBookingProcessAsync(userId, user.Role))
            {
                return BadRequest(new
                {
                    message = $"This {user.Role.ToLower()} cannot be deactivated while they still have bookings in progress. " +
                               "Wait until all of their bookings are completed or cancelled."
                });
            }
        }

        var previousStatus = user.AccountStatus;
        await _repo.UpdateStatusAsync(userId, dto.NewStatus);

        await _audit.LogAsync(userId, "ACCOUNT_STATUS_UPDATED",
            $"Status changed from {previousStatus} to {dto.NewStatus}",
            adminId, null, previousStatus, ip, dto.NewStatus);

        await _email.SendAccountStatusChangedAsync(user.Email, user.FullName, dto.NewStatus);

        return Ok(new { userId, newStatus = dto.NewStatus });
    }

    [HttpPost]
    public async Task<IActionResult> CreateInternalUser([FromBody] CreateInternalUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var requestedRole = dto.Role.Trim();
        var isAdmin = requestedRole.Equals("admin", StringComparison.OrdinalIgnoreCase);

        string normalizedRole;
        if (isAdmin)
        {
            normalizedRole = "admin";
        }
        else
        {
            var customRole = await _db.Roles.FirstOrDefaultAsync(r =>
                !r.IsSystemRole && r.RoleName.ToLower() == requestedRole.ToLower());

            if (customRole == null)
            {
                return BadRequest(new
                {
                    message = "Role must be 'admin' or an existing custom role created under Admin > Roles. " +
                               "Suppliers and Contractors must use the public registration endpoints instead."
                });
            }

            normalizedRole = customRole.RoleName;
        }

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var temporaryPassword = GenerateTemporaryPassword();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword);
        var (success, message, user) = await _repo.CreateInternalUserAsync(
            dto.FullName, dto.Email, passwordHash, normalizedRole);

        if (!success) return BadRequest(new { message });

        await _audit.LogAsync(user!.UserID, "INTERNAL_USER_CREATED",
            $"Admin created a {normalizedRole} account for {dto.Email}", adminId, null, null, ip);

        // Temporary password is only sent by email — never returned in the API response.
        var emailSent = true;
        string? emailError = null;
        try
        {
            await _email.SendInternalAccountCreatedEmailAsync(
                user.Email, user.FullName, normalizedRole, temporaryPassword);
        }
        catch (Exception ex)
        {
            emailSent = false;
            emailError = ex.Message;
            await _audit.LogAsync(user.UserID, "NOTIFICATION_DISPATCH_FAILED",
                $"SendInternalAccountCreatedEmail failed: {ex.Message}", adminId, null, null, ip);
        }

        return CreatedAtAction(nameof(GetAll), new { }, new
        {
            userId = user.UserID,
            fullName = user.FullName,
            email = user.Email,
            role = user.Role,
            accountStatus = user.AccountStatus,
            temporaryPasswordEmailed = emailSent,
            message = emailSent
                ? "Account created. A temporary password has been emailed to the user."
                : $"Account created, but the temporary-password email could not be sent ({emailError}). " +
                  "Check Email__SendGridApiKey on Render and resend credentials from support if needed."
        });
    }

    // Generates a random temporary password (12 chars, mixed case + digit + symbol) that's emailed
    // to the new internal user rather than set by the admin — the admin never gets to see or choose it.
    private static string GenerateTemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%^&*";
        const string all = upper + lower + digits + symbols;

        var bytes = new byte[12];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        var chars = new char[12];
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = symbols[bytes[3] % symbols.Length];
        for (var i = 4; i < chars.Length; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Shuffle so the guaranteed character classes aren't always in the same positions.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = bytes[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }

    [HttpPatch("{userId}/role")]
    public async Task<IActionResult> UpdateRole(int userId, [FromBody] UpdateUserRoleDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var user = await _repo.GetByIdAsync(userId);
        if (user == null) return NotFound(new { message = "User not found." });

        if (user.Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
            || user.Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Supplier and Contractor accounts cannot have their role changed here."
            });
        }

        var requestedRole = dto.Role.Trim();
        var isAdmin = requestedRole.Equals("admin", StringComparison.OrdinalIgnoreCase);

        string canonicalRole;
        if (isAdmin)
        {
            canonicalRole = "admin";
        }
        else
        {
            var customRole = await _db.Roles.FirstOrDefaultAsync(r =>
                !r.IsSystemRole && r.RoleName.ToLower() == requestedRole.ToLower());

            if (customRole == null)
            {
                return BadRequest(new
                {
                    message = "Role must be 'admin' or an existing custom role created under Admin > Roles."
                });
            }

            canonicalRole = customRole.RoleName;
        }

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var previousRole = user.Role;

        var (success, message) = await _repo.UpdateRoleAsync(userId, canonicalRole);
        if (!success) return BadRequest(new { message });

        await _audit.LogAsync(userId, "USER_ROLE_CHANGED",
            $"Role changed from {previousRole} to {canonicalRole}", adminId, null, previousRole, ip, canonicalRole);

        return Ok(new { userId, newRole = canonicalRole });
    }

    [HttpGet("{userId}/audit-log")]
    public async Task<IActionResult> GetAuditLog(int userId)
    {
        var logs = await _db.AuditLogs
            .Where(a => a.UserID == userId)
            .OrderByDescending(a => a.Timestamp)
            .Take(50)
            .Select(a => new
            {
                auditID = a.AuditID,
                transactionType = a.TransactionType,
                description = a.Description,
                timestamp = a.Timestamp
            })
            .ToListAsync();

        return Ok(logs);
    }
}
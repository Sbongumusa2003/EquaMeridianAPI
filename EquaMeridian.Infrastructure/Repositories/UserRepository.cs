using EquaMeridian.DTOs.User;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public async Task<(IEnumerable<UserDto> Users, int TotalCount)> GetAllAsync(
        string? search, string? role, string? status, int page, int pageSize)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u =>
                u.FullName.Contains(search) || u.Email.Contains(search) ||
                (u.CompanyName != null && u.CompanyName.Contains(search)));

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(u => u.Role == role);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(u => u.AccountStatus == status);

        var total = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedDate)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(u => new UserDto
            {
                UserID = u.UserID,
                FullName = u.FullName,
                Email = u.Email,
                Role = u.Role,
                AccountStatus = u.AccountStatus,
                CompanyName = u.CompanyName,
                CreatedDate = u.CreatedDate,
                LastLoginDate = u.LastLoginDate
            }).ToListAsync();

        return (users, total);
    }

    public Task<User?> GetByIdAsync(int userId)
        => _db.Users
            .Include(u => u.UserServiceAreas)
                .ThenInclude(us => us.ServiceArea)
            .FirstOrDefaultAsync(u => u.UserID == userId);

    public async Task UpdateStatusAsync(int userId, string newStatus)
    {
        var user = await _db.Users.FindAsync(userId) ?? throw new KeyNotFoundException();
        user.AccountStatus = newStatus;
        await _db.SaveChangesAsync();
    }
    public async Task<(bool Success, string Message)> UpdateProfileAsync(
        int userId, UpdateProfileDto dto)
    {
        var user = await _db.Users
            .Include(u => u.UserServiceAreas)
            .FirstOrDefaultAsync(u => u.UserID == userId);
        if (user == null) return (false, "User not found.");
        if (!string.Equals(user.Email, dto.Email, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await _db.Users.AnyAsync(u => u.Email == dto.Email && u.UserID != userId);
            if (taken) return (false, "Email already in use.");
        }

        user.FullName = dto.FullName;
        user.Email = dto.Email;
        user.CompanyName = dto.CompanyName;
        user.RegistrationNumber = dto.RegistrationNumber;
        user.BankName = dto.BankName;
        user.BankAccountName = dto.BankAccountName;
        user.BankAccountNumber = dto.BankAccountNumber;
        user.BankBranchCode = dto.BankBranchCode;
        user.BankAccountType = dto.BankAccountType;

        if (dto.ServiceAreaIds != null)
        {
            var selectedIds = dto.ServiceAreaIds.Distinct().ToList();

            var toRemove = user.UserServiceAreas
                .Where(us => !selectedIds.Contains(us.ServiceAreaID))
                .ToList();
            foreach (var usa in toRemove)
                user.UserServiceAreas.Remove(usa);

            var existingIds = user.UserServiceAreas.Select(us => us.ServiceAreaID).ToHashSet();
            foreach (var id in selectedIds.Where(id => !existingIds.Contains(id)))
                user.UserServiceAreas.Add(new UserServiceArea { UserID = userId, ServiceAreaID = id });
        }

        await _db.SaveChangesAsync();

        return (true, $"User {user.FullName} successfully updated.");
    }
    public async Task<(bool Success, string Message)> DeactivateAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        // Block self-deactivation while any booking is still in progress (not Completed/Cancelled).
        if (await HasOpenBookingProcessAsync(userId, user.Role))
            return (false,
                "Cannot deactivate account while you have bookings still in progress. " +
                "Complete or cancel those bookings first.");

        var hasActiveListings = await _db.Listings.AnyAsync(
            l => l.SupplierID == userId && l.AvailabilityStatus == "Active");

        if (hasActiveListings)
            return (false,
                "Cannot deactivate account: you have active listings. Please deactivate them first.");

        user.AccountStatus = "Inactive";
        await _db.SaveChangesAsync();

        return (true, $"User {user.FullName} account successfully deactivated.");
    }

    /// <summary>
    /// True when the user is party to a booking that has started and is not Completed/Cancelled.
    /// Used to block account deactivation (self and admin) during the booking process.
    /// </summary>
    public async Task<bool> HasOpenBookingProcessAsync(int userId, string role)
    {
        var isContractor = role.Equals("Contractor", StringComparison.OrdinalIgnoreCase);
        var isSupplier = role.Equals("Supplier", StringComparison.OrdinalIgnoreCase);
        if (!isContractor && !isSupplier) return false;

        return await _db.Bookings.AnyAsync(b =>
            (isContractor ? b.ContractorID == userId : b.SupplierID == userId)
            && b.Status != "Completed"
            && b.Status != "Cancelled");
    }

    public async Task<(bool Success, string Message, User? User)> CreateInternalUserAsync(
        string fullName, string email, string passwordHash, string role)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists) return (false, "A user with this email already exists.", null);

        var user = new User
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            Role = role,
            AccountStatus = "Active",
            CreatedDate = AppTime.Now
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return (true, $"{role} account created for {fullName}.", user);
    }

    public async Task<(bool Success, string Message)> UpdateRoleAsync(int userId, string newRole)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return (false, "User not found.");

        user.Role = newRole;
        await _db.SaveChangesAsync();

        return (true, $"Role updated to {newRole} for {user.FullName}.");
    }
}
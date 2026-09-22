using EquaMeridian.DTOs.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

[ApiController]
[Route("api/users/me")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly IUserRepository _repo;
    private readonly IAuditService _audit;
    public ProfileController(IUserRepository repo, IAuditService audit)
    {
        _repo = repo;
        _audit = audit;
    }

    private int UserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> GetMyAccount()
    {
        var user = await _repo.GetByIdAsync(UserId);
        if (user == null)
            return NotFound(new { message = "Account details could not be found. Please contact support." });

        return Ok(new
        {
            user.UserID,
            user.FullName,
            user.Email,
            user.Role,
            user.AccountStatus,
            user.CompanyName,
            user.RegistrationNumber,
            user.CreatedDate,
            user.LastLoginDate,
            user.TwoFactorEnabled,
            user.BankName,
            user.BankAccountName,
            user.BankAccountNumber,
            user.BankBranchCode,
            user.BankAccountType,
            ServiceAreaIds = user.UserServiceAreas.Select(us => us.ServiceAreaID).ToList(),
            ServiceAreaNames = user.UserServiceAreas.Select(us => us.ServiceArea.Name).ToList()
        });
    }
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var previousUser = await _repo.GetByIdAsync(UserId);
        if (previousUser == null) return NotFound();

        var previousSnapshot = $"Name:{previousUser.FullName}, Email:{previousUser.Email}";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message) = await _repo.UpdateProfileAsync(UserId, dto);

        if (!success)
        {
            if (message.Contains("Email")) return Conflict(new { message });
            return NotFound(new { message });
        }

        await _audit.LogAsync(UserId, "PROFILE_UPDATED",
            "User updated their profile", null, null,
            previousSnapshot, ip, $"Name:{dto.FullName}, Email:{dto.Email}");

        return Ok(new { message });
    }
    [HttpPatch("deactivate")]
    public async Task<IActionResult> DeactivateAccount()
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, message) = await _repo.DeactivateAsync(UserId);

        if (!success)
        {
            if (message.Contains("not found")) return NotFound(new { message });
            return BadRequest(new { message });
        }

        await _audit.LogAsync(UserId, "ACCOUNT_DEACTIVATED",
            "User deactivated their own account", null, null, "Active", ip, "Inactive");

        return Ok(new { message });
    }
}
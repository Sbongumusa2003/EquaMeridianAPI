using EquaMeridian.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth)
    {
        _auth = auth;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        try
        {
            var result = await _auth.LoginAsync(dto, ip);
            if (result == null)
                return Unauthorized(new { message = "Incorrect email or password." });

            if (result.RequiresOtp)
                return Ok(new { requiresOtp = true, otpReference = result.OtpReference, expiresAt = result.OtpExpiresAt });

            return Ok(result.Login);
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(403, new { message = $"Account is {ex.Message}." });
        }
    }

    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _auth.VerifyOtpAsync(dto, ip);
        if (result == null)
            return Unauthorized(new { message = "Code is invalid, expired, or already used." });

        return Ok(result);
    }

    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var outcome = await _auth.ResendOtpAsync(dto, ip);

        return outcome.Status switch
        {
            ResendOtpStatus.NotFound => Unauthorized(new { message = "This verification session has expired. Please log in again." }),
            ResendOtpStatus.TooSoon => StatusCode(429, new { message = "Please wait before requesting another code." }),
            _ => Ok(outcome.Response)
        };
    }

    /// <summary>Enable or disable two-factor login for the current account. Requires the current password.</summary>
    [Authorize]
    [HttpPost("two-factor/enable")]
    public async Task<IActionResult> EnableTwoFactor([FromBody] ToggleTwoFactorRequest dto)
        => await SetTwoFactor(dto, enabled: true);

    [Authorize]
    [HttpPost("two-factor/disable")]
    public async Task<IActionResult> DisableTwoFactor([FromBody] ToggleTwoFactorRequest dto)
        => await SetTwoFactor(dto, enabled: false);

    private async Task<IActionResult> SetTwoFactor(ToggleTwoFactorRequest dto, bool enabled)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var (success, message) = await _auth.SetTwoFactorEnabledAsync(userId, enabled, dto.Password);
        return success ? Ok(new { message }) : BadRequest(new { message });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        await _auth.LogoutAsync(userId, token);
        return Ok(new { message = "Logged out successfully." });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _auth.ForgotPasswordAsync(dto.Email, ip);
        return Ok(result);
    }

    /// <summary>Completes password reset using the single-use token from the email reset link.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, message) = await _auth.ResetPasswordAsync(dto, ip);
        if (!success) return BadRequest(new { message });
        return Ok(new { message });
    }
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = FormatModelStateErrors(ModelState) });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, message) = await _auth.RegisterAsync(dto, ip);

        if (!success) return RegistrationFailure(message);
        return Ok(new { message });
    }

    [HttpPost("register/supplier")]
    [RequestSizeLimit(50_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RegisterSupplier(
        [FromForm] RegisterSupplierRequest dto,
        List<IFormFile>? documents)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = FormatModelStateErrors(ModelState) });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var (success, message) = await _auth.RegisterSupplierAsync(dto, documents, ip);

        if (!success) return RegistrationFailure(message);
        return Ok(new { message });
    }

    /// <summary>
    /// 409 only for uniqueness collisions; other registration failures are 400.
    /// </summary>
    private static IActionResult RegistrationFailure(string message)
    {
        var lower = (message ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("already exists") || lower.Contains("already registered") || lower.Contains("already in use"))
            return new ConflictObjectResult(new { message });
        return new BadRequestObjectResult(new { message });
    }

    private static string FormatModelStateErrors(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState)
    {
        var msgs = modelState.Values
            .SelectMany(v => v.Errors)
            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Exception?.Message : e.ErrorMessage)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToList();
        return msgs.Count > 0
            ? string.Join(" ", msgs!)
            : "One or more validation errors occurred.";
    }
}
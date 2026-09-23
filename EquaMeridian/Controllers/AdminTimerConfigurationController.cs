using EquaMeridian.DTOs.Timers;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/timer-configuration")]
[Authorize(Policy = "AdminOnly")]
public class AdminTimerConfigurationController : ControllerBase
{
    private readonly ITimerConfigurationRepository _repo;
    private readonly IAuditService _audit;
    private readonly AppDbContext _db;

    public AdminTimerConfigurationController(ITimerConfigurationRepository repo, IAuditService audit, AppDbContext db)
    {
        _repo = repo;
        _audit = audit;
        _db = db;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await _repo.GetAsync());

    [HttpGet("/api/session-settings")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSessionSettings()
    {
        var cfg = await _repo.GetAsync();
        return Ok(new SessionSettingsDto { SessionIdleMinutes = cfg.SessionIdleMinutes > 0 ? cfg.SessionIdleMinutes : 30 });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateTimerConfigurationRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var updated = await _repo.UpdateAsync(AdminId, dto);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(AdminId, "TIMER_CONFIG_UPDATED",
            $"Timer configuration updated. Quote expiry every {dto.IntervalMinutes} min, " +
            $"expire quotes after {dto.QuoteExpiryHours} h (enabled: {dto.IsEnabled}). " +
            $"Session idle logout set to {dto.SessionIdleMinutes} minute(s) for all users.",
            AdminId, null, null, ip);

        return Ok(updated);
    }

    [HttpPost("run-now")]
    public async Task<IActionResult> RunNow()
    {
        // Temporarily disabled - SQL Server stored procedure not available on PostgreSQL yet
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(AdminId, "QUOTE_EXPIRY_RUN_MANUALLY",
            "Admin tried to manually run quote-expiry (feature temporarily disabled on PostgreSQL).",
            AdminId, null, null, ip);

        return Ok(new { expiredCount = 0, message = "Feature temporarily disabled on PostgreSQL" });
    }
}
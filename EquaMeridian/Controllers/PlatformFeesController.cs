using EquaMeridian.DTOs.Fees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/platform-fees")]
[Authorize(Policy = "AdminOnly")]
public class PlatformFeesController : ControllerBase
{
    private readonly IFeeConfigurationRepository _repo;
    private readonly IAuditService _audit;

    public PlatformFeesController(IFeeConfigurationRepository repo, IAuditService audit)
    { _repo = repo; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var config = await _repo.GetCurrentAsync();
        return Ok(config);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateFeeConfigurationDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.MaxFee < dto.MinFee)
            return BadRequest(new { message = "Maximum fee must be greater than or equal to minimum fee." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (updated, previousValuesJson) = await _repo.UpdateAsync(dto, adminId);

        await _audit.LogAsync(null, "PLATFORM_FEES_UPDATED",
            "Platform fee configuration updated.",
            adminId, null, previousValuesJson, ip);

        return Ok(updated);
    }
}

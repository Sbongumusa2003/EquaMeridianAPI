using EquaMeridian.DTOs.Fees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/discount-tiers")]
[Authorize(Policy = "AdminOnly")]
public class AdminDiscountTiersController : ControllerBase
{
    private readonly IDiscountTierRepository _repo;
    private readonly IAuditService _audit;

    public AdminDiscountTiersController(IDiscountTierRepository repo, IAuditService audit)
    { _repo = repo; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tiers = await _repo.GetAllAsync();
        return Ok(tiers);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertDiscountTierDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.MaxDays.HasValue && dto.MaxDays < dto.MinDays)
            return BadRequest(new { message = "Maximum days must be greater than or equal to minimum days." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var created = await _repo.CreateAsync(dto, adminId);

        await _audit.LogAsync(null, "DISCOUNT_TIER_CREATED",
            $"Discount tier created: {dto.MinDays}-{(dto.MaxDays?.ToString() ?? "+")} days = {dto.DiscountPercent}%",
            adminId, null, null, ip);

        return Ok(created);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertDiscountTierDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        if (dto.MaxDays.HasValue && dto.MaxDays < dto.MinDays)
            return BadRequest(new { message = "Maximum days must be greater than or equal to minimum days." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var updated = await _repo.UpdateAsync(id, dto, adminId);
        if (updated == null) return NotFound();

        await _audit.LogAsync(null, "DISCOUNT_TIER_UPDATED",
            $"Discount tier {id} updated: {dto.MinDays}-{(dto.MaxDays?.ToString() ?? "+")} days = {dto.DiscountPercent}%",
            adminId, null, null, ip);

        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var deleted = await _repo.DeleteAsync(id);
        if (!deleted) return NotFound();

        await _audit.LogAsync(null, "DISCOUNT_TIER_DELETED", $"Discount tier {id} deleted", adminId, null, null, ip);

        return NoContent();
    }
}

using EquaMeridian.DTOs.Payouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/payouts")]
[Authorize(Policy = "AdminOnly")]
public class AdminPayoutsController : ControllerBase
{
    private readonly IPayoutRepository _repo;
    private readonly IAuditService _audit;

    public AdminPayoutsController(IPayoutRepository repo, IAuditService audit)
    {
        _repo = repo;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (payouts, total) = await _repo.GetAllAsync(search, status, page, pageSize);
        return Ok(new { payouts, totalCount = total, page, pageSize });
    }

    [HttpGet("{payoutId}")]
    public async Task<IActionResult> GetById(int payoutId)
    {
        var payout = await _repo.GetByIdAsync(payoutId);
        return payout == null ? NotFound() : Ok(payout);
    }

    [HttpPatch("{payoutId}/status")]
    public async Task<IActionResult> ProcessPayout(int payoutId, [FromBody] ProcessPayoutDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var result = await _repo.ProcessAsync(payoutId, dto.NewStatus, dto.AdministratorNotes, dto.DeclineReason, adminId);
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "NotFound" => NotFound(new { message = result.Error }),
                "AlreadyProcessed" => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error })
            };
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(null, "PAYOUT_PROCESSED",
            $"Payout #{payoutId} marked as {dto.NewStatus}.",
            adminId, null, "Pending", ip, dto.NewStatus);

        return Ok(result.Payout);
    }
}

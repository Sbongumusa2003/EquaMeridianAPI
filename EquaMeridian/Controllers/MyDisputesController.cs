using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

// Separate from DisputesController (admin-only, unscoped, /api/admin/disputes) and
// BookingDisputesController (raise-only, /api/bookings-deliveries/{bookingId}/disputes).
// Before this, a Contractor/Supplier had no way at all to look up a dispute after raising
// it — only Admin could list or view disputes.
[ApiController]
[Route("api/my-disputes")]
[Authorize]
public class MyDisputesController : ControllerBase
{
    private readonly IDisputeRepository _repo;

    public MyDisputesController(IDisputeRepository repo) => _repo = repo;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (!Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
            && !Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var (disputes, total) = await _repo.GetForUserAsync(UserId, page, pageSize);
        return Ok(new { disputes, totalCount = total, page, pageSize });
    }

    [HttpGet("{disputeId}")]
    public async Task<IActionResult> GetById(int disputeId)
    {
        if (!Role.Equals("Contractor", StringComparison.OrdinalIgnoreCase)
            && !Role.Equals("Supplier", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var dispute = await _repo.GetForPartyAsync(disputeId, UserId);
        return dispute == null ? NotFound() : Ok(dispute);
    }
}

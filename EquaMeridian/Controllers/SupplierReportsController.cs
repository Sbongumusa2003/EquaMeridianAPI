using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

/// <summary>
/// A supplier's own version of the admin revenue report: monthly bookings, gross revenue,
/// platform commission taken, and net payout — scoped to their own invoices only. Backs the
/// charts on the supplier dashboard.
/// </summary>
[ApiController]
[Route("api/supplier/reports")]
[Authorize(Policy = "SupplierOnly")]
public class SupplierReportsController : ControllerBase
{
    private readonly ISupplierReportingRepository _repo;
    public SupplierReportsController(ISupplierReportingRepository repo) => _repo = repo;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var report = await _repo.GetMySummaryAsync(UserId, from, to);
        return Ok(report);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Policy = "AdminOnly")]
[HasPermission("Dashboard.View")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardRepository _repo;

    public DashboardController(IDashboardRepository repo) => _repo = repo;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var summary = await _repo.GetSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("charts")]
    public async Task<IActionResult> GetCharts(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? days)
    {
        var effectiveToDate = toDate?.Date ?? AppTime.Now.Date;
        var effectiveFromDate = fromDate?.Date ?? effectiveToDate.AddDays(-(days ?? 30) + 1);

        if (effectiveFromDate > effectiveToDate)
            return BadRequest(new { message = "The start of the date range must be before the end." });

        var charts = await _repo.GetChartsAsync(effectiveFromDate, effectiveToDate);
        return Ok(charts);
    }
}

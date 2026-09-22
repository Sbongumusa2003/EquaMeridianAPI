using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/admin/bookings")]
[Authorize(Roles = "admin")]
public class AdminBookingsController : ControllerBase
{
    private readonly IBookingRepository _repo;

    public AdminBookingsController(IBookingRepository repo)
    { _repo = repo; }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null)
    {
        var result = await _repo.GetAllForAdminAsync(page, pageSize, search, status);
        return Ok(new
        {
            bookings = result.Bookings,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize
        });
    }
}

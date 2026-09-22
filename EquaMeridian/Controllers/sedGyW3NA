using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/service-areas")]
[AllowAnonymous]
public class ServiceAreasController : ControllerBase
{
    private readonly AppDbContext _db;

    public ServiceAreasController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var areas = await _db.ServiceAreas
            .OrderBy(s => s.Name)
            .Select(s => new { s.ServiceAreaID, s.Name })
            .ToListAsync();

        return Ok(areas);
    }
}

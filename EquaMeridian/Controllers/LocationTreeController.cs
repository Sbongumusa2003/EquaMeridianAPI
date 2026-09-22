using EquaMeridian.DTOs.LocationTree;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

// A tree of data (Province -> City -> Suburb) backed by real relational tables, with full
// add/update/delete from the frontend at every level. GET is public (used to populate cascading
// location dropdowns across the site); mutation is admin-only.
[ApiController]
[Route("api/location-tree")]
[AllowAnonymous]
public class LocationTreeController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public LocationTreeController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetTree()
    {
        var tree = await _db.Provinces
            .OrderBy(p => p.Name)
            .Select(p => new ProvinceDto
            {
                ProvinceID = p.ProvinceID,
                Name = p.Name,
                Cities = p.Cities.OrderBy(c => c.Name).Select(c => new CityDto
                {
                    CityID = c.CityID,
                    Name = c.Name,
                    Suburbs = c.Suburbs.OrderBy(s => s.Name)
                        .Select(s => new SuburbDto { SuburbID = s.SuburbID, Name = s.Name })
                        .ToList()
                }).ToList()
            })
            .ToListAsync();

        return Ok(tree);
    }

    // --- Province ---

    [HttpPost("provinces")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateProvince([FromBody] UpsertNodeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required." });

        if (await _db.Provinces.AnyAsync(p => p.Name.ToLower() == dto.Name.Trim().ToLower()))
            return BadRequest(new { message = "A province with this name already exists." });

        var province = new Province { Name = dto.Name.Trim() };
        _db.Provinces.Add(province);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_PROVINCE_CREATED", $"Province \"{province.Name}\" added", AdminId, null, null, Ip);
        return Ok(new ProvinceDto { ProvinceID = province.ProvinceID, Name = province.Name });
    }

    [HttpPut("provinces/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateProvince(int id, [FromBody] UpsertNodeDto dto)
    {
        var province = await _db.Provinces.FindAsync(id);
        if (province == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required." });

        var oldName = province.Name;
        province.Name = dto.Name.Trim();
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_PROVINCE_UPDATED", $"Province \"{oldName}\" renamed to \"{province.Name}\"", AdminId, null, null, Ip);
        return Ok(new ProvinceDto { ProvinceID = province.ProvinceID, Name = province.Name });
    }

    // Referential integrity: a province can only be deleted while it has no cities under it.
    [HttpDelete("provinces/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteProvince(int id)
    {
        var province = await _db.Provinces.Include(p => p.Cities).FirstOrDefaultAsync(p => p.ProvinceID == id);
        if (province == null) return NotFound();

        if (province.Cities.Count > 0)
            return BadRequest(new { message = $"Cannot delete \"{province.Name}\" — it still has {province.Cities.Count} city/cities under it. Delete those first." });

        _db.Provinces.Remove(province);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_PROVINCE_DELETED", $"Province \"{province.Name}\" deleted", AdminId, null, null, Ip);
        return NoContent();
    }

    // --- City ---

    [HttpPost("cities")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateCity([FromBody] CreateCityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required." });

        var province = await _db.Provinces.FindAsync(dto.ProvinceID);
        if (province == null) return BadRequest(new { message = "Invalid province." });

        if (await _db.Cities.AnyAsync(c => c.ProvinceID == dto.ProvinceID && c.Name.ToLower() == dto.Name.Trim().ToLower()))
            return BadRequest(new { message = "A city with this name already exists in this province." });

        var city = new City { Name = dto.Name.Trim(), ProvinceID = dto.ProvinceID };
        _db.Cities.Add(city);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_CITY_CREATED", $"City \"{city.Name}\" added under \"{province.Name}\"", AdminId, null, null, Ip);
        return Ok(new CityDto { CityID = city.CityID, Name = city.Name });
    }

    [HttpPut("cities/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateCity(int id, [FromBody] UpsertNodeDto dto)
    {
        var city = await _db.Cities.FindAsync(id);
        if (city == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required." });

        var oldName = city.Name;
        city.Name = dto.Name.Trim();
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_CITY_UPDATED", $"City \"{oldName}\" renamed to \"{city.Name}\"", AdminId, null, null, Ip);
        return Ok(new CityDto { CityID = city.CityID, Name = city.Name });
    }

    [HttpDelete("cities/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteCity(int id)
    {
        var city = await _db.Cities.Include(c => c.Suburbs).FirstOrDefaultAsync(c => c.CityID == id);
        if (city == null) return NotFound();

        if (city.Suburbs.Count > 0)
            return BadRequest(new { message = $"Cannot delete \"{city.Name}\" — it still has {city.Suburbs.Count} suburb(s) under it. Delete those first." });

        _db.Cities.Remove(city);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_CITY_DELETED", $"City \"{city.Name}\" deleted", AdminId, null, null, Ip);
        return NoContent();
    }

    // --- Suburb ---

    [HttpPost("suburbs")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateSuburb([FromBody] CreateSuburbDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required." });

        var city = await _db.Cities.FindAsync(dto.CityID);
        if (city == null) return BadRequest(new { message = "Invalid city." });

        if (await _db.Suburbs.AnyAsync(s => s.CityID == dto.CityID && s.Name.ToLower() == dto.Name.Trim().ToLower()))
            return BadRequest(new { message = "A suburb with this name already exists in this city." });

        var suburb = new Suburb { Name = dto.Name.Trim(), CityID = dto.CityID };
        _db.Suburbs.Add(suburb);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_SUBURB_CREATED", $"Suburb \"{suburb.Name}\" added under \"{city.Name}\"", AdminId, null, null, Ip);
        return Ok(new SuburbDto { SuburbID = suburb.SuburbID, Name = suburb.Name });
    }

    [HttpPut("suburbs/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> UpdateSuburb(int id, [FromBody] UpsertNodeDto dto)
    {
        var suburb = await _db.Suburbs.FindAsync(id);
        if (suburb == null) return NotFound();
        if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "Name is required." });

        var oldName = suburb.Name;
        suburb.Name = dto.Name.Trim();
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_SUBURB_UPDATED", $"Suburb \"{oldName}\" renamed to \"{suburb.Name}\"", AdminId, null, null, Ip);
        return Ok(new SuburbDto { SuburbID = suburb.SuburbID, Name = suburb.Name });
    }

    // A suburb is always a leaf node — nothing references it — so it can always be deleted.
    [HttpDelete("suburbs/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> DeleteSuburb(int id)
    {
        var suburb = await _db.Suburbs.FindAsync(id);
        if (suburb == null) return NotFound();

        _db.Suburbs.Remove(suburb);
        await _db.SaveChangesAsync();

        await _audit.LogAsync(null, "LOCATION_TREE_SUBURB_DELETED", $"Suburb \"{suburb.Name}\" deleted", AdminId, null, null, Ip);
        return NoContent();
    }
}

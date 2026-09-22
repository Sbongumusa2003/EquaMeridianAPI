using EquaMeridian.DTOs.Categories;
using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/categories")]
[AllowAnonymous]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public CategoriesController(AppDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new { c.CategoryID, c.Name })
            .ToListAsync();

        return Ok(categories);
    }

    // Admin-only management below. Kept in the same controller since they operate on the same
    // small reference table; the [AllowAnonymous] GET above is unaffected.

    [HttpGet("admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        var categories = await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                CategoryID = c.CategoryID,
                Name = c.Name,
                ListingCount = _db.Listings.Count(l => l.CategoryID == c.CategoryID)
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpPost("admin")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] UpsertCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var exists = await _db.Categories.AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower());
        if (exists) return BadRequest(new { message = "A category with this name already exists." });

        var category = new Category { Name = dto.Name.Trim() };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(null, "CATEGORY_CREATED", $"Category \"{category.Name}\" created",
            adminId, null, null, HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new CategoryDto { CategoryID = category.CategoryID, Name = category.Name, ListingCount = 0 });
    }

    [HttpPut("admin/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertCategoryDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return NotFound();

        var duplicate = await _db.Categories.AnyAsync(c => c.CategoryID != id && c.Name.ToLower() == dto.Name.ToLower());
        if (duplicate) return BadRequest(new { message = "A category with this name already exists." });

        var oldName = category.Name;
        category.Name = dto.Name.Trim();
        await _db.SaveChangesAsync();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(null, "CATEGORY_UPDATED", $"Category \"{oldName}\" renamed to \"{category.Name}\"",
            adminId, null, null, HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new CategoryDto { CategoryID = category.CategoryID, Name = category.Name });
    }

    // Referential integrity: a category can only be hard-deleted while nothing references it.
    // Once even one listing uses it, deletion is blocked with a clear reason instead of silently
    // orphaning (or cascading through) that listing's CategoryID.
    [HttpDelete("admin/{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null) return NotFound();

        var listingCount = await _db.Listings.CountAsync(l => l.CategoryID == id);
        if (listingCount > 0)
        {
            return BadRequest(new
            {
                message = $"Cannot delete \"{category.Name}\" — it is still used by {listingCount} listing(s). " +
                           "Reassign or remove those listings first."
            });
        }

        var tierCount = await _db.DiscountTiers.CountAsync(t => t.CategoryID == id);
        if (tierCount > 0)
        {
            return BadRequest(new
            {
                message = $"Cannot delete \"{category.Name}\" — it has {tierCount} category-specific discount tier(s) attached. Remove those first."
            });
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _audit.LogAsync(null, "CATEGORY_DELETED", $"Category \"{category.Name}\" deleted",
            adminId, null, null, HttpContext.Connection.RemoteIpAddress?.ToString());

        return NoContent();
    }
}
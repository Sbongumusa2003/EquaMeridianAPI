using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Public marketplace browsing. Per requirement: browsing is open to unregistered (anonymous) visitors,
/// Contractors, and Admins. Suppliers must not browse the marketplace here — they only see their own
/// listings, via SupplierListingsController.
/// </summary>
[ApiController]
[Route("api/contractor/listings")]
[AllowAnonymous]
public class ContractorListingsController : ControllerBase
{
    private readonly IListingRepository _repo;
    public ContractorListingsController(IListingRepository repo) => _repo = repo;

    /// <summary>Blocks authenticated Suppliers from browsing the marketplace. Anonymous visitors,
    /// Contractors, and Admins are all allowed through.</summary>
    private IActionResult? BlockSuppliers()
    {
        if (User.Identity?.IsAuthenticated == true &&
            User.IsInRole("Supplier"))
        {
            return new ObjectResult(new
            {
                message = "Suppliers can't browse the marketplace. You can view and manage your own listings under My Listings."
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? category,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? location,
        [FromQuery] int? serviceAreaId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        var blocked = BlockSuppliers();
        if (blocked != null) return blocked;

        var (listings, total) = await _repo.GetAllAsync(
            search, category, "Active", page, pageSize, minPrice, maxPrice, location, serviceAreaId);
        return Ok(new { listings, totalCount = total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var blocked = BlockSuppliers();
        if (blocked != null) return blocked;

        var listing = await _repo.GetByIdAsync(id);
        return listing == null ? NotFound() : Ok(listing);
    }

    [HttpGet("compare")]
    public async Task<IActionResult> Compare([FromQuery] string ids)
    {
        var blocked = BlockSuppliers();
        if (blocked != null) return blocked;

        var idList = ids.Split(',')
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : (int?)null)
            .Where(n => n.HasValue)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        if (idList.Count < 2)
            return BadRequest(new { message = "Please select at least two listings to compare." });
        if (idList.Count > 4)
            return BadRequest(new { message = "Maximum 4 listings can be compared." });
        var listings = (await _repo.GetByIdsAsync(idList)).ToList();
        foreach (var id in idList)
        {
            var listing = listings.FirstOrDefault(l => l.ListingID == id);
            if (listing == null || listing.AvailabilityStatus != "Active")
                return NotFound(new { message = $"Listing {id} not found or not active." });
        }
        var ordered = idList.Select(id => listings.First(l => l.ListingID == id));
        return Ok(ordered);
    }
}
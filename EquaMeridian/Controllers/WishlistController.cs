using EquaMeridian.DTOs.Wishlist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/contractor/wishlist")]
[Authorize(Policy = "ContractorOnly")]
public class WishlistController : ControllerBase
{
    private readonly IWishlistRepository _repo;

    public WishlistController(IWishlistRepository repo)
    {
        _repo = repo;
    }

    private int ContractorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // ─── GET /api/contractor/wishlist ─────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await _repo.GetByContractorAsync(ContractorId);
        return Ok(items);
    }

    // ─── GET /api/contractor/wishlist/ids ─────────────────────────────────────
    /// <summary>Lightweight listing-ID set, used to render the filled/unfilled heart
    /// icon on browse/compare pages without fetching the full wishlist.</summary>
    [HttpGet("ids")]
    public async Task<IActionResult> GetIds()
    {
        var ids = await _repo.GetWishlistedListingIdsAsync(ContractorId);
        return Ok(ids);
    }

    // ─── POST /api/contractor/wishlist ─────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddWishlistItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.AddAsync(ContractorId, dto.ListingID);
        if (!result.Success)
            return result.Error == "Listing not found."
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });

        return Ok(result.Item);
    }

    // ─── DELETE /api/contractor/wishlist/{wishlistItemId} ──────────────────────
    [HttpDelete("{wishlistItemId}")]
    public async Task<IActionResult> Remove(int wishlistItemId)
    {
        var removed = await _repo.RemoveAsync(ContractorId, wishlistItemId);
        if (!removed) return NotFound(new { message = "Wishlist item not found." });
        return Ok(new { message = "Removed from wishlist." });
    }

    // ─── DELETE /api/contractor/wishlist/by-listing/{listingId} ────────────────
    [HttpDelete("by-listing/{listingId}")]
    public async Task<IActionResult> RemoveByListing(int listingId)
    {
        var removed = await _repo.RemoveByListingAsync(ContractorId, listingId);
        if (!removed) return NotFound(new { message = "This listing is not in your wishlist." });
        return Ok(new { message = "Removed from wishlist." });
    }
}

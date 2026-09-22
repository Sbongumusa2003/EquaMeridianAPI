using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>Public supplier storefront — Takealot-style "shop this seller".</summary>
[ApiController]
[Route("api/suppliers")]
[AllowAnonymous]
public class SupplierStorefrontController : ControllerBase
{
    private readonly IListingRepository _listings;
    public SupplierStorefrontController(IListingRepository listings) => _listings = listings;

    [HttpGet("{supplierId:int}")]
    public async Task<IActionResult> GetStorefront(int supplierId, [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
    {
        var dto = await _listings.GetSupplierStorefrontAsync(supplierId, page, pageSize);
        return dto == null ? NotFound(new { message = "Supplier not found." }) : Ok(dto);
    }
}

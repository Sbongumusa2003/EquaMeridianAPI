using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/promo")]
[AllowAnonymous]
public class PromoController : ControllerBase
{
    private readonly ICampaignRepository _campaigns;

    public PromoController(ICampaignRepository campaigns) => _campaigns = campaigns;

    /// <summary>Validate a campaign discount code for cart checkout.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] PromoValidateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new { message = "Promo code is required.", valid = false });

        var resolution = await _campaigns.ResolveDiscountCodeAsync(req.Code, req.ListingId);

        return Ok(new
        {
            valid = resolution.Valid,
            message = resolution.Message,
            campaignId = resolution.CampaignId,
            name = resolution.CampaignName,
            discountPercent = resolution.DiscountPercent,
            featuredListingId = resolution.FeaturedListingId
        });
    }
}

public class PromoValidateRequest
{
    public string Code { get; set; } = string.Empty;
    /// <summary>Optional — when set, listing-specific codes are validated against this listing.</summary>
    public int? ListingId { get; set; }
}

using EquaMeridian.DTOs.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
public class MarketingCampaignsController : ControllerBase
{
    private static readonly HashSet<string> ValidTypes = new() { "Banner", "Discount Code", "Featured Listing" };
    private static readonly HashSet<string> ValidStatuses = new() { "Draft", "Scheduled", "Active", "Paused", "Ended" };

    private readonly ICampaignRepository _repo;
    private readonly IAuditService _audit;

    public MarketingCampaignsController(ICampaignRepository repo, IAuditService audit)
    { _repo = repo; _audit = audit; }

    [HttpGet("api/campaigns/active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var campaigns = await _repo.GetActiveAsync();
        return Ok(new { campaigns });
    }

    [HttpGet("api/admin/campaigns")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (campaigns, total) = await _repo.GetAllAsync(search, type, status, page, pageSize);
        return Ok(new { campaigns, totalCount = total, page, pageSize });
    }

    [HttpGet("api/admin/campaigns/{campaignId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GetById(int campaignId)
    {
        var campaign = await _repo.GetByIdAsync(campaignId);
        return campaign == null ? NotFound() : Ok(campaign);
    }

    [HttpPost("api/admin/campaigns")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create([FromBody] CreateCampaignDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!ValidTypes.Contains(dto.Type))
            return BadRequest(new { message = "Campaign type must be one of: Banner, Discount Code, Featured Listing." });

        if (dto.EndDate <= dto.StartDate)
            return BadRequest(new { message = "End date must be after start date." });

        if (!dto.SaveAsDraft)
        {
            switch (dto.Type)
            {
                case "Banner" when string.IsNullOrWhiteSpace(dto.BannerImageURL):
                    return BadRequest(new { message = "A banner image is required." });
                case "Discount Code" when dto.DiscountValue is null or <= 0 || string.IsNullOrWhiteSpace(dto.DiscountCode):
                    return BadRequest(new { message = "Discount value and discount code are required." });
                case "Featured Listing" when dto.FeaturedListingID is null:
                    return BadRequest(new { message = "A listing selection is required." });
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.DiscountCode) &&
            await _repo.IsDiscountCodeInUseAsync(dto.DiscountCode))
            return BadRequest(new { message = "This discount code is already in use." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var campaignId = await _repo.CreateAsync(dto, adminId);

        await _audit.LogAsync(null, "CAMPAIGN_CREATED",
            $"Campaign '{dto.Name}' created.", adminId, null, null, ip);

        var created = await _repo.GetByIdAsync(campaignId);
        return CreatedAtAction(nameof(GetById), new { campaignId }, created);
    }

    [HttpPut("api/admin/campaigns/{campaignId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(int campaignId, [FromBody] UpdateCampaignDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!ValidStatuses.Contains(dto.Status))
            return BadRequest(new { message = "Status must be one of: Draft, Scheduled, Active, Paused, Ended." });

        if (dto.EndDate <= dto.StartDate)
            return BadRequest(new { message = "End date must be after start date." });

        if ((dto.Status is "Active" or "Scheduled") && dto.EndDate.Date < AppTime.Now.Date)
            return BadRequest(new { message = "An end date on or after today is required to activate this campaign." });

        if (!string.IsNullOrWhiteSpace(dto.DiscountCode) &&
            await _repo.IsDiscountCodeInUseAsync(dto.DiscountCode, campaignId))
            return BadRequest(new { message = "This discount code is already in use." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var updated = await _repo.UpdateAsync(campaignId, dto, adminId);
        if (!updated) return NotFound();

        await _audit.LogAsync(null, "CAMPAIGN_UPDATED",
            $"Campaign #{campaignId} updated.", adminId, null, null, ip);

        return Ok(await _repo.GetByIdAsync(campaignId));
    }

    [HttpDelete("api/admin/campaigns/{campaignId}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int campaignId)
    {
        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, priorStatus, name) = await _repo.DeleteAsync(campaignId, adminId);
        if (!success) return NotFound();

        await _audit.LogAsync(null, "CAMPAIGN_DELETED",
            $"Campaign '{name}' (previously '{priorStatus}') deleted.", adminId, null, priorStatus, ip);

        return Ok(new { message = "Campaign deleted successfully." });
    }

    /// <summary>Upload a banner image; returns a relative URL suitable for BannerImageURL.</summary>
    [HttpPost("api/admin/campaigns/upload-banner")]
    [Authorize(Policy = "AdminOnly")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadBanner([FromForm] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please choose an image file." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Banner image must be JPG, PNG, WEBP or GIF." });
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "Banner image must be 5 MB or smaller." });

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "campaign-banners");
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, name);
        await using (var stream = System.IO.File.Create(path))
            await file.CopyToAsync(stream);

        // Public relative path — StaticFiles middleware should map /uploads.
        var url = $"/uploads/campaign-banners/{name}";
        return Ok(new { url, fileName = name });
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Serves listing images stored in PostgreSQL. Required on Render because the local
/// uploads/ folder is ephemeral and is wiped on every redeploy/restart.
/// </summary>
[ApiController]
[Route("api/media")]
public class MediaController : ControllerBase
{
    private readonly IListingImageRepository _images;

    public MediaController(IListingImageRepository images)
    {
        _images = images;
    }

    [AllowAnonymous]
    [HttpGet("listing-images/{imageId:int}")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetListingImage(int imageId)
    {
        var result = await _images.GetContentAsync(imageId);
        if (result is null)
            return NotFound();

        var (content, contentType) = result.Value;
        return File(content, contentType);
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

[ApiController]
[Route("api/admin/content-rules/blocked-terms")]
[Authorize(Policy = "AdminOnly")]
public class AdminContentRulesController : ControllerBase
{
    private readonly IContentModerationService _moderation;
    private readonly IAuditService _audit;

    public AdminContentRulesController(IContentModerationService moderation, IAuditService audit)
    {
        _moderation = moderation;
        _audit = audit;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _moderation.GetBlockedTermRecordsAsync());

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddBlockedTermRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (success, error) = await _moderation.AddBlockedTermAsync(dto.Term, AdminId);
        if (!success) return Conflict(new { message = error });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(AdminId, "BLOCKED_TERM_ADDED", $"Blocked term added", AdminId, null, null, ip);

        return Ok(new { message = "Term added." });
    }

    [HttpDelete("{blockedTermId}")]
    public async Task<IActionResult> Remove(int blockedTermId)
    {
        var (success, error) = await _moderation.RemoveBlockedTermAsync(blockedTermId);
        if (!success) return NotFound(new { message = error });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(AdminId, "BLOCKED_TERM_REMOVED", $"Blocked term #{blockedTermId} removed",
            AdminId, null, null, ip);

        return NoContent();
    }
}

public class AddBlockedTermRequest
{
    [Required]
    [MaxLength(100)]
    public string Term { get; set; } = string.Empty;
}

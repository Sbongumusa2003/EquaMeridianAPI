using EquaMeridian.DTOs.Listings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/listings")]
[Authorize(Policy = "AdminOnly")]
public class AdminListingsController : ControllerBase
{
    private readonly IListingRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public AdminListingsController(IListingRepository repo,
                                   IAuditService audit,
                                   IEmailService email,
                                   INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? category,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (listings, total) = await _repo.GetAllAsync(search, category, status, page, pageSize);
        return Ok(new { listings, totalCount = total, page, pageSize });
    }

    [HttpGet("{listingId}")]
    public async Task<IActionResult> GetById(int listingId)
    {
        var listing = await _repo.GetByIdAsync(listingId);
        return listing == null ? NotFound() : Ok(listing);
    }

    [HttpPatch("{listingId}/status")]
    public async Task<IActionResult> UpdateStatus(
        int listingId, [FromBody] UpdateListingStatusDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (dto.NewStatus == "Suspended" && string.IsNullOrWhiteSpace(dto.SuspensionReason))
            return BadRequest(new { message = "Suspension reason is required." });

        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null) return NotFound();

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var previous = listing.AvailabilityStatus;

        var (ok, error) = await _repo.UpdateStatusAsync(listingId, dto.NewStatus);
        if (!ok)
            return BadRequest(new { message = error ?? "Could not update listing status." });

        await _audit.LogAsync(listing.SupplierID, "LISTING_STATUS_UPDATED",
            dto.SuspensionReason ?? $"Status changed to {dto.NewStatus}",
            adminId, listingId, previous, ip, dto.NewStatus);

        await _email.SendListingStatusChangedAsync(
            "", listing.SupplierName, listingId, dto.NewStatus, dto.SuspensionReason);

        return Ok(new { listingId, newStatus = dto.NewStatus });
    }

    [HttpPost("{listingId}/approve")]
    public async Task<IActionResult> Approve(int listingId)
    {
        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null) return NotFound();
        if (listing.AvailabilityStatus != "Pending")
            return BadRequest(new { message = "Only listings in Pending Review can be approved." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (ok, error) = await _repo.ApproveAsync(listingId, adminId);
        if (!ok)
            return BadRequest(new { message = error ?? "Could not approve this listing." });

        await _audit.LogAsync(listing.SupplierID, "LISTING_APPROVED",
            $"Listing {listingId} approved and is now live",
            adminId, listingId, listing.AvailabilityStatus, ip, "Active");

        await _email.SendListingStatusChangedAsync(
            "", listing.SupplierName, listingId, "Active", null);

        await _notifications.CreateAsync(listing.SupplierID, "ListingApproved",
            "Listing Approved", $"'{listing.ListingTitle}' has been approved and is now live.",
            "Listing", listingId);

        return Ok(new { listingId, newStatus = "Active" });
    }

    [HttpPost("{listingId}/reject")]
    public async Task<IActionResult> Reject(int listingId, [FromBody] RejectListingDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null) return NotFound();
        if (listing.AvailabilityStatus != "Pending")
            return BadRequest(new { message = "Only listings in Pending Review can be rejected." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _repo.RejectAsync(listingId, dto.Reason, adminId);

        await _audit.LogAsync(listing.SupplierID, "LISTING_REJECTED",
            dto.Reason, adminId, listingId, listing.AvailabilityStatus, ip, "Rejected");

        await _email.SendListingStatusChangedAsync(
            "", listing.SupplierName, listingId, "Rejected", dto.Reason);

        await _notifications.CreateAsync(listing.SupplierID, "ListingRejected",
            "Listing Rejected", $"'{listing.ListingTitle}' was rejected: {dto.Reason}",
            "Listing", listingId);

        return Ok(new { listingId, newStatus = "Rejected" });
    }

    [HttpPost("{listingId}/request-changes")]
    public async Task<IActionResult> RequestChanges(int listingId, [FromBody] RequestListingChangesDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var listing = await _repo.GetByIdAsync(listingId);
        if (listing == null) return NotFound();
        if (listing.AvailabilityStatus != "Pending")
            return BadRequest(new { message = "Only listings in Pending Review can have changes requested." });

        var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        await _repo.RequestChangesAsync(listingId, dto.Comments, adminId);

        await _audit.LogAsync(listing.SupplierID, "LISTING_CHANGES_REQUESTED",
            dto.Comments, adminId, listingId, listing.AvailabilityStatus, ip, "Draft");

        await _email.SendListingStatusChangedAsync(
            "", listing.SupplierName, listingId, "Draft", dto.Comments);

        await _notifications.CreateAsync(listing.SupplierID, "ListingChangesRequested",
            "Changes Requested", $"Admin requested changes to '{listing.ListingTitle}': {dto.Comments}",
            "Listing", listingId);

        return Ok(new { listingId, newStatus = "Draft" });
    }
}

using EquaMeridian.DTOs.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using static EquaMeridian.DTOs.Reviews.ReviewDto;

[ApiController]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly INotificationRepository _notifications;

    public ReviewsController(
        IReviewRepository repo, IAuditService audit, IEmailService email, IConfiguration config, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _config = config; _notifications = notifications; }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private int EditWindowDays => _config.GetValue<int?>("Reviews:EditWindowDays") ?? 30;

    [HttpPost("api/reviews")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> Create([FromBody] CreateReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.CreateAsync(UserId, dto);

        if (!result.Success)
            return ToErrorResponse(result);

        await _audit.LogAsync(UserId, "Review_Created",
            $"Contractor submitted review #{result.Review!.ReviewID} for booking #{dto.BookingID}.",
            null, result.Review.MachineryID, null, ip,
            JsonSerializer.Serialize(new { result.Review.OverallRating, result.Review.Title }));

        await _email.SendReviewSubmittedEmailAsync(
            result.SupplierEmail, result.SupplierName, dto.BookingID, result.Machinery,
            result.Review.OverallRating, result.Review.Title);

        await _notifications.CreateAsync(result.SupplierID, "ReviewSubmitted",
            "New Review",
            $"You received a {result.Review.OverallRating}-star review for \"{result.Machinery}\".",
            "Listing", result.Review.MachineryID, emailUser: false);
        return Ok(new
        {
            message = "Review submitted successfully. Thank you for your feedback.",
            review = result.Review
        });
    }

    [HttpGet("api/listings/{listingId}/reviews")]
    [AllowAnonymous]
    public async Task<IActionResult> GetForListing(
        int listingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 3,
        [FromQuery] string? sortBy = "recent", [FromQuery] int? starFilter = null)
    {
        var result = await _repo.GetForListingAsync(listingId, page, pageSize, sortBy, starFilter);
        string? message = result.TotalCount == 0
            ? (starFilter.HasValue
                ? "No reviews match this filter."
                : "No reviews yet for this listing.")
            : null;

        return Ok(new
        {
            summary = result.Summary,
            reviews = result.Reviews,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            message
        });
    }

    [HttpGet("api/reviews/mine")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> GetMine()
    {
        var reviews = await _repo.GetMineAsync(UserId, EditWindowDays);
        return Ok(new { reviews });
    }
    [HttpPatch("api/reviews/{reviewId}")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> Update(int reviewId, [FromBody] UpdateReviewDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.UpdateAsync(reviewId, UserId, dto, EditWindowDays);

        if (!result.Success)
            return ToErrorResponse(result);

        await _audit.LogAsync(UserId, "Review_Edited",
            $"Contractor edited review #{reviewId}.",
            null, result.Review!.MachineryID, null, ip,
            JsonSerializer.Serialize(new { result.Review.OverallRating, result.Review.Title }));

        await _email.SendReviewUpdatedEmailAsync(
            result.SupplierEmail, result.SupplierName, reviewId, result.Machinery, result.Review.OverallRating);

        await _notifications.CreateAsync(result.SupplierID, "ReviewUpdated",
            "Review Updated",
            $"A review for \"{result.Machinery}\" was updated (now {result.Review.OverallRating} stars).",
            "Listing", result.Review.MachineryID, emailUser: false);
        return Ok(new
        {
            message = "Review updated successfully.",
            review = result.Review
        });
    }

    [HttpDelete("api/reviews/{reviewId}")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> Delete(int reviewId)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.DeleteAsync(reviewId, UserId);

        if (!result.Success)
            return ToErrorResponse(result);

        await _audit.LogAsync(UserId, "Review_Deleted",
            $"Contractor deleted review #{reviewId}.",
            null, null, null, ip);

        await _email.SendReviewDeletedEmailAsync(
            result.SupplierEmail, result.SupplierName, reviewId, result.Machinery);

        await _notifications.CreateAsync(result.SupplierID, "ReviewDeleted",
            "Review Removed",
            $"A review for \"{result.Machinery}\" was removed by the contractor who wrote it.", emailUser: false);
        return Ok(new { message = "Review deleted." });
    }

    private IActionResult ToErrorResponse(ReviewActionResult result) => result.ErrorType switch
    {
        "NotFound" => NotFound(new { message = result.Error }),
        "Conflict" => Conflict(new { message = result.Error }),
        _ => BadRequest(new { message = result.Error })
    };
}

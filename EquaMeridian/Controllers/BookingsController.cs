using EquaMeridian.DTOs.Bookings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/bookings-deliveries")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IBookingRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly INotificationRepository _notifications;

    public BookingsController(IBookingRepository repo, IAuditService audit, IEmailService email, INotificationRepository notifications)
    { _repo = repo; _audit = audit; _email = email; _notifications = notifications; }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null, [FromQuery] string? status = null,
        [FromQuery] DateTime? dateFrom = null, [FromQuery] DateTime? dateTo = null)
    {
        var result = await _repo.GetAllForUserAsync(UserId, Role, page, pageSize, search, status, dateFrom, dateTo);
        var message = result.TotalCount == 0
            ? (string.IsNullOrWhiteSpace(search) && string.IsNullOrWhiteSpace(status) && dateFrom == null && dateTo == null
                ? "You haven't made any bookings yet. Browse available machinery to get started."
                : "No bookings match your search/filters.")
            : null;

        return Ok(new
        {
            bookings = result.Bookings,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            summaryCards = result.SummaryCards,
            message
        });
    }
    [HttpGet("{bookingId}")]
    public async Task<IActionResult> GetDeliveryDetail(int bookingId)
    {
        var detail = await _repo.GetDeliveryDetailAsync(bookingId, UserId, Role);
        if (detail == null) return NotFound();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(UserId, "DELIVERY_ADDRESS_VIEWED",
            $"User viewed the delivery address for booking #{bookingId}.",
            null, null, null, ip);

        return Ok(detail);
    }

    [HttpPut("{bookingId}/delivery-address")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> UpdateDeliveryAddress(int bookingId, [FromBody] UpdateDeliveryAddressDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var updated = await _repo.UpdateDeliveryAddressAsync(bookingId, UserId, dto.DeliveryAddress);
        if (!updated) return BadRequest(new { message = "Delivery address could not be updated for this booking." });

        await _audit.LogAsync(UserId, "DELIVERY_ADDRESS_UPDATED",
            $"Contractor updated the delivery address for booking #{bookingId}.",
            null, null, null, ip, JsonSerializer.Serialize(new { dto.DeliveryAddress }));

        return NoContent();
    }

    [HttpPost("{bookingId}/confirm-delivery")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> ConfirmDelivery(int bookingId, [FromBody] ConfirmDeliveryDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.ConfirmDeliveryAsync(bookingId, UserId, dto);

        if (!result.Success)
            return result.Error == "Booking not found." ? NotFound() : BadRequest(new { message = result.Error });

        var delivery = result.Delivery!;

        await _audit.LogAsync(UserId, "DELIVERY_CONFIRMED",
            $"Contractor confirmed delivery for booking #{bookingId}.",
            null, null, null, ip,
            JsonSerializer.Serialize(new { delivery.DeliveryStatus, delivery.DeliveryDate }));

        await _email.SendDeliveryConfirmedEmailAsync(
            result.SupplierEmail, result.SupplierName, bookingId, delivery.Machinery);

        return Ok(new { delivery = result.Delivery });
    }

    /// <summary>Supplier marks the machine prepped/ready for the contractor to collect.</summary>
    [HttpPost("{bookingId}/ready-for-pickup")]
    [Authorize(Policy = "SupplierOnly")]
    public async Task<IActionResult> MarkReadyForPickup(int bookingId)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.MarkReadyForPickupAsync(bookingId, UserId);

        if (!result.Success)
            return result.Error == "Booking not found." ? NotFound(new { message = result.Error }) : BadRequest(new { message = result.Error });

        await _audit.LogAsync(UserId, "BOOKING_READY_FOR_PICKUP",
            $"Supplier marked booking #{bookingId} ready for pickup.", null, null, null, ip);

        await _email.SendReadyForPickupEmailAsync(result.ContractorEmail, result.ContractorName, bookingId, result.Machinery);
        await _notifications.CreateAsync(result.ContractorID, "ReadyForPickup",
            "Ready for pickup",
            $"\"{result.Machinery}\" is ready for pickup for booking #{bookingId}.",
            "Booking", bookingId, emailUser: false);
        return Ok(new { message = "Booking marked ready for pickup." });
    }

    /// <summary>Supplier marks a requested return's collection as arranged.</summary>
    [HttpPost("{bookingId}/ready-for-return-pickup")]
    [Authorize(Policy = "SupplierOnly")]
    public async Task<IActionResult> MarkReadyForReturnPickup(int bookingId)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.MarkReadyForReturnPickupAsync(bookingId, UserId);

        if (!result.Success)
            return result.Error == "Booking not found." ? NotFound(new { message = result.Error }) : BadRequest(new { message = result.Error });

        await _audit.LogAsync(UserId, "BOOKING_READY_FOR_RETURN_PICKUP",
            $"Supplier marked booking #{bookingId} ready for return pickup.", null, null, null, ip);

        await _email.SendReadyForReturnPickupEmailAsync(result.ContractorEmail, result.ContractorName, bookingId, result.Machinery);
        await _notifications.CreateAsync(result.ContractorID, "ReadyForReturnPickup",
            "Return pickup arranged",
            $"Collection has been arranged for \"{result.Machinery}\" (booking #{bookingId}).",
            "Booking", bookingId, emailUser: false);
        return Ok(new { message = "Booking marked ready for return pickup." });
    }

    /// <summary>The Takealot-style tracking timeline for a booking — available to either party.</summary>
    [HttpGet("{bookingId}/tracking")]
    public async Task<IActionResult> GetTracking(int bookingId)
    {
        var tracking = await _repo.GetTrackingAsync(bookingId, UserId, Role);
        if (tracking == null) return NotFound();

        return Ok(tracking);
    }

    [HttpPost("{bookingId}/request-return")]
    [Authorize(Policy = "ContractorOnly")]
    public async Task<IActionResult> RequestReturn(int bookingId, [FromBody] RequestReturnDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.RequestReturnAsync(bookingId, UserId, dto);

        if (!result.Success)
        {
            return result.Error == "Booking not found."
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }

        await _audit.LogAsync(UserId, "RETURN_REQUESTED",
            $"Contractor requested a return for booking #{bookingId}.",
            null, null, null, ip,
            JsonSerializer.Serialize(new { dto.ReturnReason, dto.PreferredPickupDate, result.EarlyReturnFeeApplies }));

        await _email.SendReturnRequestedEmailAsync(
            result.SupplierEmail, result.SupplierName, bookingId, result.Machinery,
            dto.PreferredPickupDate, dto.PickupTimeWindow, dto.PickupLocation, dto.ReturnReason);

        return Ok(new
        {
            message = "Return request submitted successfully.",
            returnRequestId = result.ReturnRequestID,
            earlyReturnFeeApplies = result.EarlyReturnFeeApplies
        });
    }

    [HttpPost("{bookingId}/confirm-return")]
    [Authorize(Policy = "SupplierOnly")]
    public async Task<IActionResult> ConfirmReturn(
        int bookingId, [FromForm] ConfirmReturnDto dto, [FromForm] List<IFormFile>? photos)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        List<string> photoPaths;
        try
        {
            photoPaths = await SavePhotosAsync(bookingId, photos);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.ConfirmReturnAsync(bookingId, UserId, dto, photoPaths);

        if (!result.Success)
        {
            return result.Error == "Booking not found."
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        }

        await _audit.LogAsync(UserId, "RETURN_CONFIRMED",
            $"Supplier confirmed return for booking #{bookingId} with condition '{dto.Condition}'.",
            null, null, null, ip,
            JsonSerializer.Serialize(new { dto.Condition, result.DepositDeductionID }));

        await _email.SendReturnConfirmedEmailAsync(
            result.ContractorEmail, result.ContractorName, bookingId, result.Machinery, result.Condition);

        return Ok(new
        {
            message = "Return confirmed successfully.",
            depositDeductionId = result.DepositDeductionID
        });
    }

    private async Task<List<string>> SavePhotosAsync(int bookingId, List<IFormFile>? photos)
    {
        var paths = new List<string>();
        if (photos == null || photos.Count == 0) return paths;

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
        var uploadPath = Path.Combine(
            Directory.GetCurrentDirectory(), "uploads", "return-photos", bookingId.ToString());
        Directory.CreateDirectory(uploadPath);

        foreach (var photo in photos)
        {
            var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                throw new ArgumentException("Photo evidence must be a JPG or PNG image.");

            var storedFileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(uploadPath, storedFileName);

            using (var stream = System.IO.File.Create(fullPath))
                await photo.CopyToAsync(stream);

            paths.Add(fullPath);
        }

        return paths;
    }

    /// <summary>Either party cancels a booking before fulfilment begins. See
    /// IBookingRepository.CancelAsync for the exact eligibility window and side effects
    /// (lease cancelled, unpaid invoice voided, paid invoice routed to a Refund request).</summary>
    [HttpPost("{bookingId}/cancel")]
    public async Task<IActionResult> Cancel(int bookingId, [FromBody] CancelBookingDto dto)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _repo.CancelAsync(bookingId, UserId, Role, dto.Reason);

        if (!result.Success)
            return result.Error == "Booking not found." ? NotFound(new { message = result.Error }) : BadRequest(new { message = result.Error });

        await _audit.LogAsync(UserId, "BOOKING_CANCELLED",
            $"{result.CancelledByRole} cancelled booking #{bookingId}.", null, null, null, ip);

        await _notifications.CreateAsync(result.OtherPartyUserID, "BookingCancelled",
            "Booking Cancelled",
            $"Booking #{bookingId} for \"{result.Machinery}\" has been cancelled by the {result.CancelledByRole.ToLower()}."
            + (result.RefundRequestCreated ? " A refund request has been created for processing." : ""),
            "Booking", bookingId);

        await _email.SendBookingCancelledEmailAsync(
            result.OtherPartyEmail, result.OtherPartyName, bookingId, result.Machinery, result.CancelledByRole, dto.Reason);

        return Ok(new
        {
            message = "Booking cancelled.",
            cancellationFeeApplies = result.CancellationFeeApplies,
            refundRequestCreated = result.RefundRequestCreated
        });
    }
}

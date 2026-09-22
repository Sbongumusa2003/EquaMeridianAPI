using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingCalendarController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingCalendarController(AppDbContext db) => _db = db;

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string Role => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    [HttpGet("{bookingId}/calendar.ics")]
    public async Task<IActionResult> GetCalendarEvent(int bookingId)
    {
        var booking = await _db.Bookings
            .Include(b => b.Listing)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingID == bookingId);

        if (booking == null) return NotFound(new { message = "Booking not found." });

        var isParty = booking.ContractorID == UserId || booking.SupplierID == UserId;
        var isAdmin = Role.Equals("admin", StringComparison.OrdinalIgnoreCase);
        if (!isParty && !isAdmin) return Forbid();

        var bytes = IcsWriter.GenerateBookingEvent(
            booking.BookingID,
            $"EquaMeridian Booking #{booking.BookingID} — {booking.Listing.ListingTitle}",
            booking.RentalStartDate,
            booking.RentalEndDate,
            booking.DeliveryAddress,
            $"Rental of \"{booking.Listing.ListingTitle}\" (status: {booking.Status}). " +
            $"Managed via EquaMeridian, booking #{booking.BookingID}."
        );

        return File(bytes, "text/calendar", $"equameridian-booking-{booking.BookingID}.ics");
    }
}

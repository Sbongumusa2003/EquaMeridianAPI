using EquaMeridian.DTOs.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

[ApiController]
[Route("api/contractor/cart")]
[Authorize(Policy = "ContractorOnly")]
public class CartController : ControllerBase
{
    private readonly ICartRepository _repo;
    private readonly IAuditService _audit;
    private readonly IEmailService _email;
    private readonly IUserRepository _users;
    private readonly INotificationRepository _notifications;

    public CartController(ICartRepository repo, IAuditService audit, IEmailService email, IUserRepository users,
        INotificationRepository notifications)
    {
        _repo = repo;
        _audit = audit;
        _email = email;
        _users = users;
        _notifications = notifications;
    }

    private int ContractorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetCart([FromQuery] string? promoCode = null)
    {
        var cart = await _repo.GetCartAsync(ContractorId, promoCode);
        return Ok(cart);
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var contractor = await _users.GetByIdAsync(ContractorId);
        if (contractor == null || contractor.AccountStatus != "Active")
            return StatusCode(403, new
            {
                message = "Your verification documents have not been approved yet. " +
                           "You can browse listings, but you cannot add items to the cart or check out until an admin approves your documents."
            });

        var (success, error) = await _repo.AddItemAsync(ContractorId, dto);
        if (!success) return BadRequest(new { message = error });

        var cart = await _repo.GetCartAsync(ContractorId);
        return Ok(cart);
    }

    [HttpPut("items/{cartItemId}")]
    public async Task<IActionResult> UpdateItem(int cartItemId, [FromBody] UpdateCartItemDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (success, error) = await _repo.UpdateItemAsync(ContractorId, cartItemId, dto);
        if (!success)
            return error == "Cart item not found." ? NotFound() : BadRequest(new { message = error });

        var cart = await _repo.GetCartAsync(ContractorId);
        return Ok(cart);
    }

    [HttpDelete("items/{cartItemId}")]
    public async Task<IActionResult> RemoveItem(int cartItemId)
    {
        var removed = await _repo.RemoveItemAsync(ContractorId, cartItemId);
        if (!removed) return NotFound();

        var cart = await _repo.GetCartAsync(ContractorId);
        return Ok(cart);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequestDto? body = null)
    {
        var contractor = await _users.GetByIdAsync(ContractorId);
        if (contractor == null || contractor.AccountStatus != "Active")
            return StatusCode(403, new
            {
                message = "Your verification documents have not been approved yet. " +
                           "You can browse listings, but you cannot complete a purchase until an admin approves your documents."
            });

        var result = await _repo.CheckoutAsync(ContractorId, body?.PromoCode);
        if (!result.Success)
            return BadRequest(new { message = result.Error });

        await _audit.LogAsync(ContractorId, "CART_CHECKOUT",
            $"Contractor checked out {result.Bookings.Count} item(s) from their cart.",
            null, null, null, Ip,
            JsonSerializer.Serialize(new { BookingIds = result.Bookings.Select(b => b.BookingID) }));

        foreach (var booking in result.Bookings)
        {
            try
            {
                await _email.SendInvoiceGeneratedEmailAsync(
                    contractor.Email, contractor.FullName, booking.InvoiceID, booking.InvoiceNumber, booking.TotalAmount);
            }
            catch { /* email is best-effort */ }

            if (booking.SupplierID <= 0) continue;
            try
            {
                await _notifications.CreateAsync(booking.SupplierID, "BookingCreated",
                    "New booking",
                    $"A contractor booked \"{booking.ListingTitle}\" (booking #{booking.BookingID}). Sign the lease agreement so fulfilment can start.",
                    "Booking", booking.BookingID, emailUser: false);

                if (booking.LeaseAgreementID > 0)
                {
                    await _notifications.CreateAsync(booking.SupplierID, "LeaseAgreementSignatureRequired",
                        "Lease agreement awaiting your signature",
                        $"Lease for \"{booking.ListingTitle}\" is ready to sign. Open Lease agreements and sign so the contractor can pay and you can deliver or hand over.",
                        "LeaseAgreement", booking.LeaseAgreementID, emailUser: false);
                }
            }
            catch { /* notifications must not fail a completed checkout */ }
        }

        return Ok(result);
    }
}

using EquaMeridian.DTOs.Payouts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/supplier/payouts")]
[Authorize(Policy = "SupplierOnly")]
public class SupplierPayoutsController : ControllerBase
{
    private readonly IPayoutRepository _repo;
    private readonly IAuditService _audit;

    public SupplierPayoutsController(IPayoutRepository repo, IAuditService audit)
    {
        _repo = repo;
        _audit = audit;
    }

    private int SupplierId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("eligible-invoices")]
    public async Task<IActionResult> GetEligibleInvoices()
    {
        var invoices = await _repo.GetEligibleInvoicesAsync(SupplierId);
        return Ok(invoices);
    }

    [HttpGet]
    public async Task<IActionResult> GetOwn()
    {
        var payouts = await _repo.GetAllForSupplierAsync(SupplierId);
        return Ok(payouts);
    }

    [HttpGet("{payoutId}")]
    public async Task<IActionResult> GetById(int payoutId)
    {
        var payout = await _repo.GetByIdForSupplierAsync(payoutId, SupplierId);
        return payout == null ? NotFound() : Ok(payout);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RequestPayoutDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.CreateRequestAsync(SupplierId, dto.InvoiceID, dto.SupplierNotes);
        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "NotFound" => NotFound(new { message = result.Error }),
                "AlreadyExists" => Conflict(new { message = result.Error }),
                _ => BadRequest(new { message = result.Error })
            };
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _audit.LogAsync(SupplierId, "PAYOUT_REQUESTED",
            $"Payout requested for invoice #{dto.InvoiceID}.",
            null, null, null, ip);

        return CreatedAtAction(nameof(GetById), new { payoutId = result.Payout!.PayoutID }, result.Payout);
    }
}

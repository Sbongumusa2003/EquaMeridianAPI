using EquaMeridian.DTOs.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/contractor/refunds")]
[Authorize(Policy = "ContractorOnly")]
public class ContractorRefundsController : ControllerBase
{
    private readonly IRefundRepository _repo;
    private readonly IAuditService _audit;

    public ContractorRefundsController(IRefundRepository repo, IAuditService audit)
    {
        _repo = repo;
        _audit = audit;
    }

    private int ContractorId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetOwn()
    {
        var refunds = await _repo.GetAllForContractorAsync(ContractorId);
        return Ok(refunds);
    }

    [HttpGet("{refundId}")]
    public async Task<IActionResult> GetById(int refundId)
    {
        var refund = await _repo.GetByIdForContractorAsync(refundId, ContractorId);
        return refund == null ? NotFound() : Ok(refund);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRefundRequestDto dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var result = await _repo.CreateContractorRequestAsync(ContractorId, dto.InvoiceID, dto.Reason);
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
        await _audit.LogAsync(ContractorId, "REFUND_REQUESTED",
            $"Refund requested for invoice #{dto.InvoiceID}.",
            null, null, null, ip);

        return CreatedAtAction(nameof(GetById), new { refundId = result.Refund!.RefundID }, result.Refund);
    }
}

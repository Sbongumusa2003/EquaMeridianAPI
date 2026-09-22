using EquaMeridian.DTOs.Inspections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/supplier/inspections")]
[Authorize(Policy = "SupplierOnly")]
public class SupplierInspectionsController : ControllerBase
{
    private readonly IInspectionRepository _repo;

    public SupplierInspectionsController(IInspectionRepository repo)
    { _repo = repo; }

    private int SupplierId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var (inspections, total) = await _repo.GetAllForSupplierAsync(SupplierId, status, page, pageSize);
        return Ok(new { inspections, totalCount = total, page, pageSize });
    }

    [HttpGet("{inspectionId}/outcome")]
    public async Task<IActionResult> GetForOutcome(int inspectionId)
    {
        var inspection = await _repo.GetForOutcomeAsync(inspectionId, SupplierId);
        return inspection == null ? NotFound() : Ok(inspection);
    }

    // Req: suppliers can only VIEW inspection outcomes/statuses — confirmation is admin/contractor
    // only (see InspectionsController and ContractorInspectionsController). No POST/confirm here.
}

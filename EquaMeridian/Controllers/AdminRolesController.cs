using EquaMeridian.DTOs.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Policy = "AdminOnly")]
public class AdminRolesController : ControllerBase
{
    private readonly IRoleRepository _repo;
    private readonly IAuditService _audit;

    public AdminRolesController(IRoleRepository repo, IAuditService audit)
    {
        _repo = repo;
        _audit = audit;
    }

    private int AdminId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _repo.GetAllRolesAsync());

    [HttpGet("{roleId}")]
    public async Task<IActionResult> GetById(int roleId)
    {
        var role = await _repo.GetRoleByIdAsync(roleId);
        return role == null ? NotFound(new { message = "Role not found." }) : Ok(role);
    }

    [HttpGet("~/api/admin/permissions")]
    public async Task<IActionResult> GetAllPermissions() => Ok(await _repo.GetAllPermissionsAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest dto)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var (success, error, role) = await _repo.CreateRoleAsync(dto);
        if (!success) return Conflict(new { message = error });

        await _audit.LogAsync(AdminId, "ROLE_CREATED",
            $"Role \"{dto.RoleName}\" created with permissions: {string.Join(", ", dto.PermissionKeys)}",
            AdminId, null, null, Ip);

        return CreatedAtAction(nameof(GetById), new { roleId = role!.RoleID }, role);
    }

    [HttpPut("{roleId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(int roleId, [FromBody] UpdateRolePermissionsRequest dto)
    {
        var (success, error, role) = await _repo.UpdatePermissionsAsync(roleId, dto);
        if (!success) return NotFound(new { message = error });

        await _audit.LogAsync(AdminId, "ROLE_PERMISSIONS_UPDATED",
            $"Role \"{role!.RoleName}\" permissions set to: {string.Join(", ", dto.PermissionKeys)}",
            AdminId, null, null, Ip);

        return Ok(role);
    }

    [HttpDelete("{roleId}")]
    public async Task<IActionResult> Delete(int roleId)
    {
        var (success, error) = await _repo.DeleteRoleAsync(roleId);
        if (!success) return BadRequest(new { message = error });

        await _audit.LogAsync(AdminId, "ROLE_DELETED", $"Role #{roleId} deleted", AdminId, null, null, Ip);
        return NoContent();
    }
}

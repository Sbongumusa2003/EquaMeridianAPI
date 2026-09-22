using EquaMeridian.DTOs.Roles;
using EquaMeridian.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

public class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _db;
    public RoleRepository(AppDbContext db) => _db = db;

    public async Task<List<RoleDto>> GetAllRolesAsync()
    {
        var roles = await _db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.RoleName)
            .ToListAsync();

        return roles.Select(ToDto).ToList();
    }

    public async Task<RoleDto?> GetRoleByIdAsync(int roleId)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.RoleID == roleId);

        return role == null ? null : ToDto(role);
    }

    public async Task<List<PermissionDto>> GetAllPermissionsAsync()
    {
        return await _db.Permissions
            .OrderBy(p => p.PermissionKey)
            .Select(p => new PermissionDto
            {
                PermissionID = p.PermissionID,
                PermissionKey = p.PermissionKey,
                Description = p.Description
            })
            .ToListAsync();
    }

    public async Task<(bool Success, string? Error, RoleDto? Role)> CreateRoleAsync(CreateRoleRequest dto)
    {
        var nameExists = await _db.Roles.AnyAsync(r => r.RoleName.ToLower() == dto.RoleName.ToLower());
        if (nameExists) return (false, "A role with that name already exists.", null);

        var permissions = await _db.Permissions
            .Where(p => dto.PermissionKeys.Contains(p.PermissionKey))
            .ToListAsync();

        var role = new Role
        {
            RoleName = dto.RoleName,
            Description = dto.Description,
            IsSystemRole = false,
            CreatedDate = AppTime.Now,
            RolePermissions = permissions.Select(p => new RolePermission { Permission = p }).ToList()
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        return (true, null, ToDto(role));
    }

    public async Task<(bool Success, string? Error, RoleDto? Role)> UpdatePermissionsAsync(
        int roleId, UpdateRolePermissionsRequest dto)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.RoleID == roleId);

        if (role == null) return (false, "Role not found.", null);

        var permissions = await _db.Permissions
            .Where(p => dto.PermissionKeys.Contains(p.PermissionKey))
            .ToListAsync();

        _db.RolePermissions.RemoveRange(role.RolePermissions);
        role.RolePermissions = permissions
            .Select(p => new RolePermission { RoleID = role.RoleID, PermissionID = p.PermissionID })
            .ToList();

        await _db.SaveChangesAsync();

        var updated = await GetRoleByIdAsync(roleId);
        return (true, null, updated);
    }

    public async Task<(bool Success, string? Error)> DeleteRoleAsync(int roleId)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleID == roleId);
        if (role == null) return (false, "Role not found.");
        if (role.IsSystemRole) return (false, "System roles cannot be deleted.");

        var inUse = await _db.Users.AnyAsync(u => u.Role.ToLower() == role.RoleName.ToLower());
        if (inUse) return (false, "Role is still assigned to one or more users and cannot be deleted.");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    private static RoleDto ToDto(Role role) => new()
    {
        RoleID = role.RoleID,
        RoleName = role.RoleName,
        Description = role.Description,
        IsSystemRole = role.IsSystemRole,
        CreatedDate = role.CreatedDate,
        PermissionKeys = role.RolePermissions.Select(rp => rp.Permission.PermissionKey).OrderBy(k => k).ToList()
    };
}

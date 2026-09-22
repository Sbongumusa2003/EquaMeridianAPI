using EquaMeridian.DTOs.Roles;

public interface IRoleRepository
{
    Task<List<RoleDto>> GetAllRolesAsync();
    Task<RoleDto?> GetRoleByIdAsync(int roleId);
    Task<List<PermissionDto>> GetAllPermissionsAsync();

    Task<(bool Success, string? Error, RoleDto? Role)> CreateRoleAsync(CreateRoleRequest dto);
    Task<(bool Success, string? Error, RoleDto? Role)> UpdatePermissionsAsync(int roleId, UpdateRolePermissionsRequest dto);
    Task<(bool Success, string? Error)> DeleteRoleAsync(int roleId);
}

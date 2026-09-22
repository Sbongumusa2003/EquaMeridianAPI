using EquaMeridian.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionKey { get; }
    public PermissionRequirement(string permissionKey) => PermissionKey = permissionKey;
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;
    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var roleName = context.User.FindFirst(ClaimTypes.Role)?.Value
            ?? context.User.FindFirst("role")?.Value;
        if (string.IsNullOrWhiteSpace(roleName)) return;

        if (string.Equals(roleName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var hasPermission = await db.RolePermissions
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .AnyAsync(rp => rp.Role.RoleName.ToLower() == roleName.ToLower()
                         && rp.Permission.PermissionKey == requirement.PermissionKey);

        if (hasPermission) context.Succeed(requirement);
    }
}

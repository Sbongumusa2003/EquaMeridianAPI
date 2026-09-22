using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

/// <summary>
/// Marks the caller as internal platform staff: the built-in "admin" role, or any
/// custom role assigned under Admin &gt; Roles (i.e. not Supplier / Contractor).
/// Used by the AdminOnly policy so users given a specific internal role can access
/// admin APIs instead of receiving 403 Access Denied after login.
/// </summary>
public class InternalStaffRequirement : IAuthorizationRequirement
{
}

public class InternalStaffAuthorizationHandler : AuthorizationHandler<InternalStaffRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InternalStaffRequirement requirement)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value;
        if (string.IsNullOrWhiteSpace(role))
            return Task.CompletedTask;

        var r = role.Trim();

        // Built-in full administrator
        if (r.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Custom internal roles (Finance Officer, Support, etc.) — not marketplace roles
        if (!r.Equals("Supplier", StringComparison.OrdinalIgnoreCase)
            && !r.Equals("Contractor", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

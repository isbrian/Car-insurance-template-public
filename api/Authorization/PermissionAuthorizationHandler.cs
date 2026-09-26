using Microsoft.AspNetCore.Authorization;

namespace CarInsurance.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        foreach (var role in AppRoles.All)
        {
            if (context.User.IsInRole(role) && RolePermissionMap.HasPermission(role, requirement.Permission))
            {
                context.Succeed(requirement);
                break;
            }
        }

        return Task.CompletedTask;
    }
}

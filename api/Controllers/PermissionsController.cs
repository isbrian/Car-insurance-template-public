using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize(Policy = AppPolicies.SystemAdministrator)]
public sealed class PermissionsController : ControllerBase
{
    [HttpGet("matrix")]
    public ActionResult<PermissionMatrixDto> GetMatrix()
    {
        var roles = AppRoles.All.ToArray();
        var permissions = AppPermissions.All.ToArray();
        var matrix = roles
            .Select(role => new PermissionMatrixRoleDto(
                role,
                permissions.Where(permission => RolePermissionMap.HasPermission(role, permission)).ToArray()))
            .ToArray();

        return Ok(new PermissionMatrixDto(roles, permissions, matrix));
    }
}

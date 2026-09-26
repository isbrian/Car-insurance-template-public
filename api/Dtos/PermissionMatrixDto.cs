namespace CarInsurance.Api.Dtos;

public sealed record PermissionMatrixRoleDto(
    string Role,
    IReadOnlyList<string> Permissions);

public sealed record PermissionMatrixDto(
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<PermissionMatrixRoleDto> Matrix);

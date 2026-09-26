using Microsoft.AspNetCore.Authorization;

namespace CarInsurance.Api.Authorization;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

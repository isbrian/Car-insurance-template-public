namespace CarInsurance.Api.Authorization;

public static class RolePermissionMap
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> PermissionsByRole =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [AppRoles.Sales] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.QuoteCreate,
                AppPermissions.ApplicationSubmit,
                AppPermissions.EndorsementRequest,
                AppPermissions.PiiUnmask
            },
            [AppRoles.Underwriter] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.QuoteCreate,
                AppPermissions.ApplicationSubmit,
                AppPermissions.UnderwritingDecide,
                AppPermissions.PolicyIssue,
                AppPermissions.EndorsementReview,
                AppPermissions.PiiUnmask
            },
            [AppRoles.Claims] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.ClaimSettle,
                AppPermissions.PiiUnmask
            },
            [AppRoles.Manager] = new HashSet<string>(AppPermissions.All, StringComparer.Ordinal),
            [AppRoles.SystemAdministrator] = new HashSet<string>(StringComparer.Ordinal)
            {
                AppPermissions.QuoteCreate,
                AppPermissions.ApplicationSubmit,
                AppPermissions.PiiUnmask,
                AppPermissions.AuditRead,
                AppPermissions.UnderwritingRuleManage
            }
        };

    public static bool HasPermission(string role, string permission) =>
        PermissionsByRole.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static IReadOnlySet<string> GetPermissions(string role) =>
        PermissionsByRole.TryGetValue(role, out var permissions)
            ? permissions
            : new HashSet<string>(StringComparer.Ordinal);
}

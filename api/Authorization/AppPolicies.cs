namespace CarInsurance.Api.Authorization;

public static class AppPolicies
{
    public const string QuoteCreate = "Permission:QuoteCreate";
    public const string ApplicationSubmit = "Permission:ApplicationSubmit";
    public const string UnderwritingDecide = "Permission:UnderwritingDecide";
    public const string PolicyIssue = "Permission:PolicyIssue";
    public const string EndorsementRequest = "Permission:EndorsementRequest";
    public const string EndorsementReview = "Permission:EndorsementReview";
    public const string ClaimSettle = "Permission:ClaimSettle";
    public const string PiiUnmask = "Permission:PiiUnmask";
    public const string AuditRead = "Permission:AuditRead";
    public const string UnderwritingRuleManage = "Permission:UnderwritingRuleManage";
    public const string SystemAdministrator = "Role:SystemAdministrator";

    public static readonly IReadOnlyDictionary<string, string> PermissionPolicies =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [QuoteCreate] = AppPermissions.QuoteCreate,
            [ApplicationSubmit] = AppPermissions.ApplicationSubmit,
            [UnderwritingDecide] = AppPermissions.UnderwritingDecide,
            [PolicyIssue] = AppPermissions.PolicyIssue,
            [EndorsementRequest] = AppPermissions.EndorsementRequest,
            [EndorsementReview] = AppPermissions.EndorsementReview,
            [ClaimSettle] = AppPermissions.ClaimSettle,
            [PiiUnmask] = AppPermissions.PiiUnmask,
            [AuditRead] = AppPermissions.AuditRead,
            [UnderwritingRuleManage] = AppPermissions.UnderwritingRuleManage
        };
}

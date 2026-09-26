namespace CarInsurance.Api.Authorization;

public static class AppPermissions
{
    public const string QuoteCreate = "quote.create";
    public const string ApplicationSubmit = "application.submit";
    public const string UnderwritingDecide = "underwriting.decide";
    public const string PolicyIssue = "policy.issue";
    public const string EndorsementRequest = "endorsement.request";
    public const string EndorsementReview = "endorsement.review";
    public const string ClaimSettle = "claim.settle";
    public const string PiiUnmask = "pii.unmask";
    public const string AuditRead = "audit.read";
    public const string UnderwritingRuleManage = "underwriting-rule.manage";

    public static readonly IReadOnlyList<string> All =
    [
        QuoteCreate,
        ApplicationSubmit,
        UnderwritingDecide,
        PolicyIssue,
        EndorsementRequest,
        EndorsementReview,
        ClaimSettle,
        PiiUnmask,
        AuditRead,
        UnderwritingRuleManage
    ];
}

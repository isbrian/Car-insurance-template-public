namespace CarInsurance.Api.Authorization;

public static class AppRoles
{
    public const string Sales = "業務員";
    public const string Underwriter = "核保員";
    public const string Claims = "理賠員";
    public const string Manager = "主管";
    public const string SystemAdministrator = "系統管理員";

    public static readonly IReadOnlyList<string> All =
    [
        Sales,
        Underwriter,
        Claims,
        Manager,
        SystemAdministrator
    ];
}

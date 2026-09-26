namespace CarInsurance.Api.Entities;

public sealed class UnderwritingResult
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public int RuleId { get; set; }

    public UnderwritingRule Rule { get; set; } = null!;

    public string RuleCode { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public DateTime EvaluatedAtUtc { get; set; }
}

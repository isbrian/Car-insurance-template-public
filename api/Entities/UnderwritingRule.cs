namespace CarInsurance.Api.Entities;

public sealed class UnderwritingRule
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string ConditionType { get; set; } = string.Empty;

    public string ConditionJson { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<UnderwritingResult> Results { get; set; } = new List<UnderwritingResult>();
}

namespace CarInsurance.Api.Entities;

public class UnderwritingCase
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public string OverallAction { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = string.Empty;

    public string? Opinion { get; set; }

    public string? Decision { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

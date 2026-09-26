namespace CarInsurance.Api.Entities;

public sealed class PolicyLifecycle
{
    public int Id { get; set; }

    public int QuoteId { get; set; }

    public Quote Quote { get; set; } = null!;

    public int? ApplicationId { get; set; }

    public InsuranceApplication? Application { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? LastTransition { get; set; }

    public string? LastTransitionReason { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

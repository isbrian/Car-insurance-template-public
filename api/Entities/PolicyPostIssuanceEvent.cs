namespace CarInsurance.Api.Entities;

public sealed class PolicyPostIssuanceEvent
{
    public int Id { get; set; }

    public int PolicyId { get; set; }

    public Policy Policy { get; set; } = null!;

    public string Step { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}

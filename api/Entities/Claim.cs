namespace CarInsurance.Api.Entities;

public sealed class Claim
{
    public int Id { get; set; }

    public string ClaimNumber { get; set; } = string.Empty;

    public int PolicyId { get; set; }

    public Policy Policy { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public DateTime AccidentDate { get; set; }

    public string AccidentLocation { get; set; } = string.Empty;

    public string AccidentDescription { get; set; } = string.Empty;

    public decimal? ClaimedAmount { get; set; }

    public string? LiabilityDecision { get; set; }

    public int? LiabilityPercentage { get; set; }

    public decimal? ApprovedAmount { get; set; }

    public string? PaymentMethod { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public string? DeniedReason { get; set; }

    public string? AssignedAdjuster { get; set; }

    public string LastTransition { get; set; } = string.Empty;

    public string? LastTransitionReason { get; set; }

    public DateTime StatusChangedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

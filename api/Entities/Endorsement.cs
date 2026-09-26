namespace CarInsurance.Api.Entities;

public sealed class Endorsement
{
    public int Id { get; set; }
    public string EndorsementNumber { get; set; } = string.Empty;
    public int PolicyId { get; set; }
    public Policy Policy { get; set; } = null!;
    public string EndorsementType { get; set; } = string.Empty;
    public DateTime RequestedEffectiveDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ChangeSummary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ReviewOpinion { get; set; }
    public long? OriginalPremiumMinorUnits { get; set; }
    public long? RecalculatedPremiumMinorUnits { get; set; }
    public long? AdjustmentAmountMinorUnits { get; set; }
    public int? CoverageDays { get; set; }
    public int? RemainingDays { get; set; }
    public string? AdjustmentCalculationRequestJson { get; set; }
    public DateTime? AdjustmentCalculatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public DateTime? ReviewingAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

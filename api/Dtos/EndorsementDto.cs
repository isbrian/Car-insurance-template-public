using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record EndorsementDto(
    int Id,
    string EndorsementNumber,
    int PolicyId,
    string PolicyNumber,
    string EndorsementType,
    DateTime RequestedEffectiveDate,
    string Reason,
    string ChangeSummary,
    string Status,
    string? ReviewOpinion,
    decimal? OriginalPremium,
    decimal? RecalculatedPremium,
    decimal? AdjustmentAmount,
    string? AdjustmentType,
    int? CoverageDays,
    int? RemainingDays,
    decimal? UnexpiredRatio,
    DateTime? AdjustmentCalculatedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime? ReviewingAtUtc,
    DateTime? DecidedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record EndorsementCreateDto(
    [Range(1, int.MaxValue)] int PolicyId,
    [Required, MaxLength(80)] string EndorsementType,
    DateTime RequestedEffectiveDate,
    [Required, MaxLength(1000)] string Reason,
    [Required, MaxLength(2000)] string ChangeSummary);

public sealed record EndorsementUpdateDto(
    [Required, MaxLength(80)] string EndorsementType,
    DateTime RequestedEffectiveDate,
    [Required, MaxLength(1000)] string Reason,
    [Required, MaxLength(2000)] string ChangeSummary);

public sealed record EndorsementDecisionDto(
    [Required, MaxLength(1000)] string ReviewOpinion);

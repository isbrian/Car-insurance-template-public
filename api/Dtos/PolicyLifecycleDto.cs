using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record PolicyLifecycleDto(
    int Id,
    int QuoteId,
    string QuoteNumber,
    int? ApplicationId,
    string? ApplicationNumber,
    string Status,
    string? LastTransition,
    string? LastTransitionReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PolicyLifecycleTransitionDto(
    [Required, MaxLength(40)] string TargetStatus,
    [Required, MaxLength(80)] string Transition,
    [MaxLength(500)] string? Reason);

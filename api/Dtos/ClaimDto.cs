using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record ClaimDto(
    int Id,
    string ClaimNumber,
    int PolicyId,
    string PolicyNumber,
    string ClaimantName,
    string LicensePlate,
    string Status,
    DateOnly AccidentDate,
    string AccidentLocation,
    string AccidentDescription,
    decimal? ClaimedAmount,
    string? LiabilityDecision,
    int? LiabilityPercentage,
    decimal? ApprovedAmount,
    string? PaymentMethod,
    DateTime? PaidAtUtc,
    string? DeniedReason,
    string? AssignedAdjuster,
    string LastTransition,
    string? LastTransitionReason,
    DateTime StatusChangedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ClaimCreateDto(
    [Range(1, int.MaxValue)] int PolicyId,
    DateOnly AccidentDate,
    [Required, MaxLength(300)] string AccidentLocation,
    [Required, MaxLength(2000)] string AccidentDescription,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? ClaimedAmount);

public sealed record ClaimUpdateDto(
    DateOnly AccidentDate,
    [Required, MaxLength(300)] string AccidentLocation,
    [Required, MaxLength(2000)] string AccidentDescription,
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal? ClaimedAmount);

public sealed record ClaimTransitionDto(
    [Required, MaxLength(40)] string TargetStatus,
    [MaxLength(100)] string? AssignedAdjuster,
    [MaxLength(1000)] string? Reason);

public sealed record ClaimLiabilityDto(
    [Required, MaxLength(500)] string Decision,
    [Range(0, 100)] int LiabilityPercentage,
    [MaxLength(1000)] string? Reason);

public sealed record ClaimApproveDto(
    [Range(typeof(decimal), "0", "79228162514264337593543950335")] decimal ApprovedAmount,
    [MaxLength(1000)] string? Reason);

public sealed record ClaimPayDto(
    [Required, MaxLength(100)] string PaymentMethod,
    [MaxLength(1000)] string? Reason);

public sealed record ClaimDenyDto(
    [Required, MaxLength(1000)] string Reason);

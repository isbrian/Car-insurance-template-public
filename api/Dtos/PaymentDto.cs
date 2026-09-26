using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record PaymentCreateDto(
    [Range(1, int.MaxValue)] int ApplicationId,
    [Required, MaxLength(20)] string PaymentFrequency,
    [Required, MaxLength(30)] string PaymentMethod);

public sealed record PaymentUpdateDto(
    [Required, MaxLength(20)] string PaymentFrequency,
    [Required, MaxLength(30)] string PaymentMethod,
    [MaxLength(1000)] string? Note);

public sealed record PaymentConfirmDto(
    [MaxLength(100)] string? ReferenceNumber,
    [MaxLength(1000)] string? Note);

public sealed record PaymentDto(
    int Id,
    string PaymentNumber,
    int ApplicationId,
    string ApplicationNumber,
    int QuoteId,
    string QuoteNumber,
    int CustomerId,
    string CustomerName,
    int VehicleId,
    string LicensePlate,
    string Status,
    string PaymentFrequency,
    string PaymentMethod,
    decimal AmountDue,
    int InstallmentCount,
    DateTime DueAtUtc,
    DateTime? PaidAtUtc,
    DateTime? CancelledAtUtc,
    string? ReferenceNumber,
    string? Note,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? PolicyLifecycleStatus);

public sealed record PaymentSyncOverdueResultDto(
    int ProcessedCount,
    int OverdueCount);

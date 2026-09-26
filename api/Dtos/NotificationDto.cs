using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record NotificationCreateDto(
    [Required, MaxLength(60)] string EventType,
    [Required, MaxLength(20)] string Channel,
    [Required, MaxLength(254)] string Recipient,
    [Required, MaxLength(200)] string Title,
    [Required, MaxLength(2000)] string Message,
    [MaxLength(60)] string? ReferenceType,
    [MaxLength(100)] string? ReferenceId);

public sealed record NotificationRetryDto(
    [MaxLength(254)] string? Recipient);

public sealed record NotificationDto(
    int Id,
    string EventType,
    string Channel,
    string Recipient,
    string Title,
    string Message,
    string Status,
    int AttemptCount,
    string? LastError,
    string? ReferenceType,
    string? ReferenceId,
    DateTime CreatedAtUtc,
    DateTime? LastAttemptAtUtc,
    DateTime? SentAtUtc,
    DateTime? ReadAtUtc);

public sealed record NotificationListResponseDto(
    IReadOnlyList<NotificationDto> Items,
    int Page,
    int PageSize,
    int Total);

using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface INotificationService
{
    Task<NotificationListResponseDto> QueryAsync(
        string? keyword,
        string? channel,
        string? status,
        string? eventType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<NotificationDto?> GetAsync(int id, CancellationToken cancellationToken);
    Task<NotificationDto> CreateAsync(NotificationCreateDto request, CancellationToken cancellationToken);
    Task<NotificationDto> RetryAsync(int id, NotificationRetryDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<NotificationDto> MarkReadAsync(int id, AuditActorContext auditActor, CancellationToken cancellationToken);
}

public sealed class NotificationValidationException(string message) : Exception(message);
public sealed class NotificationNotFoundException(string message) : Exception(message);
public sealed class NotificationConflictException(string message) : Exception(message);

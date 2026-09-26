using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface INotificationRepository
{
    Task<NotificationQueryResult> QueryAsync(
        string? keyword,
        string? channel,
        string? status,
        string? eventType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Notification?> GetAsync(int id, CancellationToken cancellationToken);
    Task<Notification?> GetForUpdateAsync(int id, CancellationToken cancellationToken);
    Task AddAsync(Notification notification, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record NotificationQueryResult(
    IReadOnlyList<Notification> Items,
    int Total);

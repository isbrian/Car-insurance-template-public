using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class NotificationRepository(CarInsuranceDbContext dbContext) : INotificationRepository
{
    public async Task<NotificationQueryResult> QueryAsync(
        string? keyword,
        string? channel,
        string? status,
        string? eventType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Notifications
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x =>
                x.Recipient.Contains(normalized) ||
                x.Title.Contains(normalized) ||
                x.Message.Contains(normalized) ||
                (x.ReferenceId != null && x.ReferenceId.Contains(normalized)));
        }

        if (!string.IsNullOrWhiteSpace(channel))
        {
            query = query.Where(x => x.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(x => x.EventType == eventType);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new NotificationQueryResult(items, total);
    }

    public Task<Notification?> GetAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Notification?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Notifications
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task AddAsync(Notification notification, CancellationToken cancellationToken) =>
        dbContext.Notifications.AddAsync(notification, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

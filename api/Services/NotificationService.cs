using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class NotificationService(
    INotificationRepository repository,
    IAuditLogService auditLogService,
    ILogger<NotificationService> logger) : INotificationService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private const string InAppChannel = "in_app";
    private const string SmsChannel = "sms";
    private const string EmailChannel = "email";

    private const string PendingStatus = "pending";
    private const string SentStatus = "sent";
    private const string FailedStatus = "failed";

    private const string DemoFailurePrefix = "fail-demo:";

    private static readonly HashSet<string> SupportedChannels =
        new(StringComparer.Ordinal) { InAppChannel, SmsChannel, EmailChannel };

    private static readonly HashSet<string> SupportedStatuses =
        new(StringComparer.Ordinal) { PendingStatus, SentStatus, FailedStatus };

    public async Task<NotificationListResponseDto> QueryAsync(
        string? keyword,
        string? channel,
        string? status,
        string? eventType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new NotificationValidationException("page 必須大於等於 1。");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new NotificationValidationException("pageSize 必須介於 1 到 100。");
        }

        var normalizedChannel = NormalizeOptional(channel);
        if (normalizedChannel is not null && !SupportedChannels.Contains(normalizedChannel))
        {
            throw new NotificationValidationException("channel 僅支援 in_app、sms、email。");
        }

        var normalizedStatus = NormalizeOptional(status);
        if (normalizedStatus is not null && !SupportedStatuses.Contains(normalizedStatus))
        {
            throw new NotificationValidationException("status 僅支援 pending、sent、failed。");
        }

        var result = await repository.QueryAsync(
            NormalizeOptional(keyword),
            normalizedChannel,
            normalizedStatus,
            NormalizeOptional(eventType),
            page,
            pageSize,
            cancellationToken);

        return new NotificationListResponseDto(
            result.Items.Select(ToDto).ToList(),
            page,
            pageSize,
            result.Total);
    }

    public async Task<NotificationDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var notification = await repository.GetAsync(id, cancellationToken);
        return notification is null ? null : ToDto(notification);
    }

    public async Task<NotificationDto> CreateAsync(
        NotificationCreateDto request,
        CancellationToken cancellationToken)
    {
        var eventType = RequireText(request.EventType, "eventType");
        var channel = RequireText(request.Channel, "channel");
        var recipient = RequireText(request.Recipient, "recipient");
        var title = RequireText(request.Title, "title");
        var message = RequireText(request.Message, "message");

        if (!SupportedChannels.Contains(channel))
        {
            throw new NotificationValidationException("channel 僅支援 in_app、sms、email。");
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            EventType = eventType,
            Channel = channel,
            Recipient = recipient,
            Title = title,
            Message = message,
            Status = PendingStatus,
            ReferenceType = NormalizeOptional(request.ReferenceType),
            ReferenceId = NormalizeOptional(request.ReferenceId),
            CreatedAtUtc = now
        };

        if (channel == InAppChannel)
        {
            MarkInAppDelivered(notification, now);
        }
        else
        {
            SimulateExternalDelivery(notification, now);
        }

        await repository.AddAsync(notification, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToDto(notification);
    }

    public async Task<NotificationDto> RetryAsync(
        int id,
        NotificationRetryDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var notification = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotificationNotFoundException($"找不到 Id={id} 的通知。");

        if (notification.Channel == InAppChannel)
        {
            throw new NotificationConflictException("站內通知不需要外部重送。");
        }

        if (notification.Status == SentStatus)
        {
            throw new NotificationConflictException("通知已成功發送，不可重複重送。");
        }

        if (notification.Status != FailedStatus)
        {
            throw new NotificationConflictException($"只有 failed 狀態可重送，目前狀態為 {notification.Status}。");
        }

        if (request.Recipient is not null)
        {
            notification.Recipient = RequireText(request.Recipient, "recipient");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeAuditSnapshot(notification);
        SimulateExternalDelivery(notification, DateTime.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "notification.retried",
            notification.Id,
            beforeJson,
            SerializeAuditSnapshot(notification),
            cancellationToken);
        return ToDto(notification);
    }

    public async Task<NotificationDto> MarkReadAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var notification = await repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotificationNotFoundException($"找不到 Id={id} 的通知。");

        if (notification.Channel != InAppChannel)
        {
            throw new NotificationConflictException("只有站內通知可標記已讀。");
        }

        if (notification.ReadAtUtc is null)
        {
            var actor = RequireAuditActor(auditActor);
            var beforeJson = SerializeAuditSnapshot(notification);
            notification.ReadAtUtc = DateTime.UtcNow;
            await repository.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(
                actor,
                "notification.read",
                notification.Id,
                beforeJson,
                SerializeAuditSnapshot(notification),
                cancellationToken);
        }

        return ToDto(notification);
    }

    private void MarkInAppDelivered(Notification notification, DateTime now)
    {
        notification.AttemptCount += 1;
        notification.LastAttemptAtUtc = now;
        notification.Status = SentStatus;
        notification.LastError = null;
        notification.SentAtUtc = now;
    }

    private void SimulateExternalDelivery(Notification notification, DateTime now)
    {
        notification.AttemptCount += 1;
        notification.LastAttemptAtUtc = now;

        if (notification.Recipient.StartsWith(DemoFailurePrefix, StringComparison.OrdinalIgnoreCase))
        {
            notification.Status = FailedStatus;
            notification.LastError = "Demo：模擬外部通知服務發送失敗。";
            notification.SentAtUtc = null;
            logger.LogWarning(
                "[Demo] 模擬 {Channel} 通知發送失敗。Recipient={Recipient}, EventType={EventType}, Attempt={AttemptCount}",
                notification.Channel,
                notification.Recipient,
                notification.EventType,
                notification.AttemptCount);
            return;
        }

        notification.Status = SentStatus;
        notification.LastError = null;
        notification.SentAtUtc = now;
        logger.LogInformation(
            "[Demo] 已模擬發送 {Channel} 通知。Recipient={Recipient}, EventType={EventType}, Attempt={AttemptCount}",
            notification.Channel,
            notification.Recipient,
            notification.EventType,
            notification.AttemptCount);
    }

    private static string SerializeAuditSnapshot(Notification notification) =>
        JsonSerializer.Serialize(
            new
            {
                notification.Channel,
                notification.Status,
                notification.AttemptCount,
                IsRead = notification.ReadAtUtc is not null
            },
            AuditJsonOptions);

    private static AuditActorContext RequireAuditActor(AuditActorContext auditActor)
    {
        var actor = auditActor.Actor.Trim();
        var role = auditActor.Role.Trim();
        var sourceIp = auditActor.SourceIp.Trim();
        if (actor.Length == 0 || role.Length == 0 || sourceIp.Length == 0)
        {
            throw new InvalidOperationException("缺少可信的稽核操作人、角色或來源 IP。");
        }

        return new AuditActorContext(actor, role, sourceIp);
    }

    private Task WriteAuditAsync(
        AuditActorContext actor,
        string action,
        int notificationId,
        string beforeJson,
        string afterJson,
        CancellationToken cancellationToken) =>
        auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                action,
                "Notification",
                notificationId.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                afterJson,
                actor.SourceIp),
            cancellationToken);

    private static string RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NotificationValidationException($"{fieldName} 不可為空白。");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static NotificationDto ToDto(Notification notification) =>
        new(
            notification.Id,
            notification.EventType,
            notification.Channel,
            notification.Recipient,
            notification.Title,
            notification.Message,
            notification.Status,
            notification.AttemptCount,
            notification.LastError,
            notification.ReferenceType,
            notification.ReferenceId,
            AsUtc(notification.CreatedAtUtc),
            notification.LastAttemptAtUtc is null ? null : AsUtc(notification.LastAttemptAtUtc.Value),
            notification.SentAtUtc is null ? null : AsUtc(notification.SentAtUtc.Value),
            notification.ReadAtUtc is null ? null : AsUtc(notification.ReadAtUtc.Value));

    private static DateTime AsUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

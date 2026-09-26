namespace CarInsurance.Api.Dtos;

public sealed record AuditLogDto(
    long Id,
    DateTime OccurredAtUtc,
    string Actor,
    string Role,
    string Action,
    string ObjectType,
    string ObjectId,
    string? BeforeJson,
    string? AfterJson,
    string SourceIp);

public sealed record AuditLogListResponseDto(
    IReadOnlyList<AuditLogDto> Items,
    int Page,
    int PageSize,
    int Total);

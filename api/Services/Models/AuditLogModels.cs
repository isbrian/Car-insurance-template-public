namespace CarInsurance.Api.Services.Models;

public sealed record AuditLogWriteRequest(
    string Actor,
    string Role,
    string Action,
    string ObjectType,
    string ObjectId,
    string? BeforeJson,
    string? AfterJson,
    string SourceIp);

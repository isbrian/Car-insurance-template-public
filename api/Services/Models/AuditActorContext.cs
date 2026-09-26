namespace CarInsurance.Api.Services.Models;

public sealed record AuditActorContext(
    string Actor,
    string Role,
    string SourceIp);

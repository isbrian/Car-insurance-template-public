namespace CarInsurance.Api.Dtos;

public sealed record PolicyPostIssuanceEventDto(
    int Id,
    int PolicyId,
    string PolicyNumber,
    string Step,
    string Title,
    string Status,
    string Detail,
    int AttemptCount,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record PolicyPostIssuanceTimelineDto(
    int PolicyId,
    string PolicyNumber,
    IReadOnlyList<PolicyPostIssuanceEventDto> Items);

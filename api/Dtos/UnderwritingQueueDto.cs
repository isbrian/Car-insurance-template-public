using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record UnderwritingQueueStatsDto(
    int PendingReviewCount,
    int AutoApprovedCount,
    int PendingDocumentsCount,
    int RejectedCount);

public sealed record UnderwritingQueueItemDto(
    int Id,
    int ApplicationId,
    string ApplicationNumber,
    string InsuredName,
    string LicensePlate,
    string ProductSummary,
    decimal TotalPremium,
    string RiskLevel,
    string Status,
    string OverallAction,
    DateTime? SubmittedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record UnderwritingQueueListDto(
    IReadOnlyList<UnderwritingQueueItemDto> Items,
    UnderwritingQueueStatsDto Stats,
    int Page,
    int PageSize,
    int Total);

public sealed record UnderwritingCaseDetailDto(
    int Id,
    int ApplicationId,
    string ApplicationNumber,
    string InsuredName,
    string LicensePlate,
    string ProductSummary,
    decimal TotalPremium,
    string RiskLevel,
    string Status,
    string OverallAction,
    string? Opinion,
    string? Decision,
    DateTime? ReviewedAtUtc,
    DateTime? SubmittedAtUtc,
    DateTime UpdatedAtUtc,
    UnderwritingEvaluationDto Evaluation);

public sealed record UnderwritingQueueSyncResultDto(
    int ProcessedCount,
    int CreatedCount,
    int UpdatedCount,
    UnderwritingQueueStatsDto Stats);

public sealed class UnderwritingDecisionRequestDto
{
    [MaxLength(1000)]
    public string? Opinion { get; set; }
}

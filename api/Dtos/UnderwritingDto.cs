using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record UnderwritingRuleDto(
    int Id,
    string Code,
    string Name,
    string ConditionType,
    string ConditionJson,
    string Action,
    string Message,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public class UnderwritingRuleCreateDto
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string ConditionType { get; set; } = string.Empty;

    [Required]
    public string ConditionJson { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}

public sealed class UnderwritingRuleUpdateDto : UnderwritingRuleCreateDto
{
}

public sealed record UnderwritingResultDto(
    int Id,
    int ApplicationId,
    int RuleId,
    string RuleCode,
    string RuleName,
    string Action,
    string Status,
    string Message,
    string Detail,
    DateTime EvaluatedAtUtc);

public sealed record UnderwritingEvaluationDto(
    int ApplicationId,
    string OverallAction,
    bool HasReject,
    bool RequiresManualReview,
    bool BlocksIssue,
    IReadOnlyList<UnderwritingResultDto> Results);

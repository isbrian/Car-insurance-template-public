using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class UnderwritingQueueService(
    IUnderwritingQueueRepository repository,
    IUnderwritingService underwritingService,
    IPolicyLifecycleService policyLifecycleService,
    IAuditLogService auditLogService) : IUnderwritingQueueService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedStatuses = new(StringComparer.Ordinal)
    {
        "pending_review",
        "pending_documents",
        "approved",
        "rejected",
        "auto_rejected",
        "auto_approved"
    };

    public async Task<UnderwritingQueueSyncResultDto> SyncAsync(CancellationToken cancellationToken)
    {
        var applications = await repository.GetSubmittedApplicationsAsync(cancellationToken);
        var created = 0;
        var updated = 0;

        foreach (var application in applications)
        {
            var evaluation = await underwritingService.EvaluateAsync(application.Id, cancellationToken)
                ?? throw new InvalidOperationException($"核保同步時找不到 ApplicationId={application.Id}。");
            var underwritingCase = await repository.GetByApplicationIdForUpdateAsync(application.Id, cancellationToken);
            var now = DateTime.UtcNow;

            if (underwritingCase is null)
            {
                underwritingCase = new UnderwritingCase
                {
                    ApplicationId = application.Id,
                    CreatedAtUtc = now
                };
                ApplyEvaluation(underwritingCase, evaluation, now);
                await repository.AddAsync(underwritingCase, cancellationToken);
                created++;
            }
            else if (!ShouldPreserveManualDecision(underwritingCase))
            {
                ApplyEvaluation(underwritingCase, evaluation, now);
                updated++;
            }

            await PrepareLifecycleForCaseAsync(underwritingCase, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new UnderwritingQueueSyncResultDto(
            applications.Count,
            created,
            updated,
            await GetStatsAsync(cancellationToken));
    }

    public async Task<UnderwritingQueueListDto> GetQueueAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ValidatePaging(page, pageSize);
        var normalizedStatus = NormalizeStatus(status);
        var (items, total) = await repository.SearchAsync(
            keyword,
            normalizedStatus,
            page,
            pageSize,
            cancellationToken);

        return new UnderwritingQueueListDto(
            items.Select(MapItem).ToList(),
            await GetStatsAsync(cancellationToken),
            page,
            pageSize,
            total);
    }

    public async Task<UnderwritingCaseDetailDto?> GetCaseAsync(int id, CancellationToken cancellationToken)
    {
        var underwritingCase = await repository.GetByIdAsync(id, cancellationToken);
        if (underwritingCase is null)
        {
            return null;
        }

        var evaluation = await underwritingService.GetResultsAsync(underwritingCase.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException($"核保案件 Id={id} 找不到對應評估資料。");
        return MapDetail(underwritingCase, evaluation);
    }

    public async Task<UnderwritingCaseDetailDto?> ReevaluateAsync(int id, CancellationToken cancellationToken)
    {
        var underwritingCase = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (underwritingCase is null)
        {
            return null;
        }
        if (underwritingCase.Status != "pending_documents")
        {
            throw new UnderwritingQueueConflictException("只有待補件案件可以重新評估。");
        }

        var evaluation = await underwritingService.EvaluateAsync(underwritingCase.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException($"重新評估時找不到 ApplicationId={underwritingCase.ApplicationId}。");
        ApplyEvaluation(underwritingCase, evaluation, DateTime.UtcNow);
        await PrepareLifecycleForCaseAsync(underwritingCase, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapDetail(underwritingCase, evaluation);
    }

    public async Task<UnderwritingCaseDetailDto?> ApproveAsync(
        int id,
        UnderwritingDecisionRequestDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var underwritingCase = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (underwritingCase is null)
        {
            return null;
        }
        if (underwritingCase.Status != "pending_review")
        {
            throw new UnderwritingQueueConflictException("只有待人工覆核案件可以核准。");
        }

        var evaluation = await underwritingService.EvaluateAsync(underwritingCase.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException($"核准時找不到 ApplicationId={underwritingCase.ApplicationId}。");
        if (evaluation.HasReject)
        {
            throw new UnderwritingQueueConflictException("案件命中拒保規則，不可人工核准。");
        }
        if (evaluation.BlocksIssue)
        {
            throw new UnderwritingQueueConflictException("案件仍命中阻擋出單規則，請先要求補件並重新評估。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDecisionSnapshot(underwritingCase);
        var now = DateTime.UtcNow;
        underwritingCase.Status = "approved";
        underwritingCase.OverallAction = evaluation.OverallAction;
        underwritingCase.RiskLevel = ResolveRiskLevel(evaluation);
        underwritingCase.Opinion = NormalizeOptionalOpinion(request.Opinion);
        underwritingCase.Decision = "approve";
        underwritingCase.ReviewedAtUtc = now;
        underwritingCase.UpdatedAtUtc = now;
        await PrepareLifecycleForCaseAsync(underwritingCase, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "underwriting.approved",
            underwritingCase.Id,
            beforeJson,
            SerializeDecisionSnapshot(underwritingCase),
            cancellationToken);
        return MapDetail(underwritingCase, evaluation);
    }

    public async Task<UnderwritingCaseDetailDto?> RequestDocumentsAsync(
        int id,
        UnderwritingDecisionRequestDto request,
        CancellationToken cancellationToken)
    {
        var underwritingCase = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (underwritingCase is null)
        {
            return null;
        }
        if (underwritingCase.Status is not ("pending_review" or "pending_documents"))
        {
            throw new UnderwritingQueueConflictException("目前案件狀態不可要求補件。");
        }

        var opinion = RequiredOpinion(request.Opinion);
        var evaluation = await underwritingService.GetResultsAsync(underwritingCase.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException($"要求補件時找不到 ApplicationId={underwritingCase.ApplicationId} 的評估資料。");
        var now = DateTime.UtcNow;
        underwritingCase.Status = "pending_documents";
        underwritingCase.OverallAction = evaluation.OverallAction;
        underwritingCase.RiskLevel = ResolveRiskLevel(evaluation);
        underwritingCase.Opinion = opinion;
        underwritingCase.Decision = "request_documents";
        underwritingCase.ReviewedAtUtc = now;
        underwritingCase.UpdatedAtUtc = now;
        await PrepareLifecycleForCaseAsync(underwritingCase, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return MapDetail(underwritingCase, evaluation);
    }

    public async Task<UnderwritingCaseDetailDto?> RejectAsync(
        int id,
        UnderwritingDecisionRequestDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var underwritingCase = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (underwritingCase is null)
        {
            return null;
        }
        if (underwritingCase.Status != "pending_review")
        {
            throw new UnderwritingQueueConflictException("只有待人工覆核案件可以退件。");
        }

        var opinion = RequiredOpinion(request.Opinion);
        var evaluation = await underwritingService.GetResultsAsync(underwritingCase.ApplicationId, cancellationToken)
            ?? throw new InvalidOperationException($"退件時找不到 ApplicationId={underwritingCase.ApplicationId} 的評估資料。");
        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDecisionSnapshot(underwritingCase);
        var now = DateTime.UtcNow;
        underwritingCase.Status = "rejected";
        underwritingCase.OverallAction = evaluation.OverallAction;
        underwritingCase.RiskLevel = ResolveRiskLevel(evaluation);
        underwritingCase.Opinion = opinion;
        underwritingCase.Decision = "reject";
        underwritingCase.ReviewedAtUtc = now;
        underwritingCase.UpdatedAtUtc = now;
        await PrepareLifecycleForCaseAsync(underwritingCase, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "underwriting.rejected",
            underwritingCase.Id,
            beforeJson,
            SerializeDecisionSnapshot(underwritingCase),
            cancellationToken);
        return MapDetail(underwritingCase, evaluation);
    }

    private static void ApplyEvaluation(
        UnderwritingCase underwritingCase,
        UnderwritingEvaluationDto evaluation,
        DateTime now)
    {
        underwritingCase.OverallAction = evaluation.OverallAction;
        underwritingCase.RiskLevel = ResolveRiskLevel(evaluation);
        underwritingCase.UpdatedAtUtc = now;

        switch (evaluation.OverallAction)
        {
            case "reject":
                underwritingCase.Status = "auto_rejected";
                underwritingCase.Decision = "auto_reject";
                underwritingCase.Opinion = "系統自動核保命中拒保規則。";
                underwritingCase.ReviewedAtUtc = now;
                break;
            case "approve":
                underwritingCase.Status = "auto_approved";
                underwritingCase.Decision = "auto_approve";
                underwritingCase.Opinion = "系統自動核保全數通過。";
                underwritingCase.ReviewedAtUtc = now;
                break;
            case "manual_review":
                underwritingCase.Status = "pending_review";
                underwritingCase.Decision = null;
                underwritingCase.Opinion = null;
                underwritingCase.ReviewedAtUtc = null;
                break;
            case "block_issue":
                underwritingCase.Status = "pending_documents";
                underwritingCase.Decision = null;
                underwritingCase.Opinion = "案件命中阻擋出單規則，待補件後重新評估。";
                underwritingCase.ReviewedAtUtc = null;
                break;
            default:
                throw new InvalidOperationException($"不支援的核保 overallAction：{evaluation.OverallAction}。");
        }
    }

    private async Task<UnderwritingQueueStatsDto> GetStatsAsync(CancellationToken cancellationToken)
    {
        var counts = await repository.GetStatusCountsAsync(cancellationToken);
        return new UnderwritingQueueStatsDto(
            GetCount(counts, "pending_review"),
            GetCount(counts, "auto_approved"),
            GetCount(counts, "pending_documents"),
            GetCount(counts, "rejected") + GetCount(counts, "auto_rejected"));
    }

    private static int GetCount(IReadOnlyDictionary<string, int> counts, string status) =>
        counts.TryGetValue(status, out var count) ? count : 0;

    private static string ResolveRiskLevel(UnderwritingEvaluationDto evaluation)
    {
        if (evaluation.HasReject)
        {
            return "high";
        }
        if (evaluation.RequiresManualReview || evaluation.BlocksIssue)
        {
            return "medium";
        }
        return "low";
    }

    private static UnderwritingQueueItemDto MapItem(UnderwritingCase underwritingCase)
    {
        var application = underwritingCase.Application;
        return new UnderwritingQueueItemDto(
            underwritingCase.Id,
            application.Id,
            application.ApplicationNumber,
            application.InsuredName ?? application.ApplicantName ?? "未填寫",
            application.Quote.Vehicle.LicensePlate,
            ResolveProductSummary(application.Quote),
            application.Quote.TotalPremium,
            underwritingCase.RiskLevel,
            underwritingCase.Status,
            underwritingCase.OverallAction,
            application.SubmittedAtUtc,
            underwritingCase.UpdatedAtUtc);
    }

    private static UnderwritingCaseDetailDto MapDetail(
        UnderwritingCase underwritingCase,
        UnderwritingEvaluationDto evaluation)
    {
        var application = underwritingCase.Application;
        return new UnderwritingCaseDetailDto(
            underwritingCase.Id,
            application.Id,
            application.ApplicationNumber,
            application.InsuredName ?? application.ApplicantName ?? "未填寫",
            application.Quote.Vehicle.LicensePlate,
            ResolveProductSummary(application.Quote),
            application.Quote.TotalPremium,
            underwritingCase.RiskLevel,
            underwritingCase.Status,
            underwritingCase.OverallAction,
            underwritingCase.Opinion,
            underwritingCase.Decision,
            underwritingCase.ReviewedAtUtc,
            application.SubmittedAtUtc,
            underwritingCase.UpdatedAtUtc,
            evaluation);
    }

    private static string ResolveProductSummary(Quote quote)
    {
        if (quote.CompulsoryPremium > 0 && quote.VoluntaryPremium > 0)
        {
            return "強制＋任意";
        }
        if (quote.CompulsoryPremium > 0)
        {
            return "強制險";
        }
        if (quote.VoluntaryPremium > 0)
        {
            return "任意險";
        }
        return "未設定";
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }
        var normalized = status.Trim().ToLowerInvariant();
        if (!SupportedStatuses.Contains(normalized))
        {
            throw new UnderwritingQueueValidationException(
                $"status 只接受：{string.Join(", ", SupportedStatuses.OrderBy(x => x))}。");
        }
        return normalized;
    }

    private static void ValidatePaging(int page, int pageSize)
    {
        if (page < 1)
        {
            throw new UnderwritingQueueValidationException("page 必須大於等於 1。");
        }
        if (pageSize is < 1 or > 100)
        {
            throw new UnderwritingQueueValidationException("pageSize 必須介於 1 到 100。");
        }
    }

    private static string RequiredOpinion(string? opinion)
    {
        var normalized = opinion?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            throw new UnderwritingQueueValidationException("核保意見為必填。");
        }
        if (normalized.Length > 1000)
        {
            throw new UnderwritingQueueValidationException("核保意見不可超過 1000 字元。");
        }
        return normalized;
    }

    private static string? NormalizeOptionalOpinion(string? opinion)
    {
        var normalized = opinion?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }
        if (normalized.Length > 1000)
        {
            throw new UnderwritingQueueValidationException("核保意見不可超過 1000 字元。");
        }
        return normalized;
    }

    private static string SerializeDecisionSnapshot(UnderwritingCase underwritingCase) =>
        JsonSerializer.Serialize(
            new
            {
                underwritingCase.Status,
                underwritingCase.OverallAction,
                underwritingCase.RiskLevel,
                underwritingCase.Opinion,
                underwritingCase.Decision
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
        int underwritingCaseId,
        string beforeJson,
        string afterJson,
        CancellationToken cancellationToken) =>
        auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                action,
                "UnderwritingCase",
                underwritingCaseId.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                afterJson,
                actor.SourceIp),
            cancellationToken);

    private async Task PrepareLifecycleForCaseAsync(
        UnderwritingCase underwritingCase,
        CancellationToken cancellationToken)
    {
        var (targetStatus, transition, reason) = underwritingCase.Status switch
        {
            "pending_review" => (PolicyLifecycleService.Statuses.PendingUnderwriting, "underwriting_pending_review", "案件待人工核保。"),
            "pending_documents" => (PolicyLifecycleService.Statuses.PendingDocuments, "underwriting_documents_required", underwritingCase.Opinion ?? "核保要求補件。"),
            "approved" => (PolicyLifecycleService.Statuses.PendingPayment, "underwriting_approved", underwritingCase.Opinion ?? "人工核保通過。"),
            "auto_approved" => (PolicyLifecycleService.Statuses.PendingPayment, "underwriting_auto_approved", underwritingCase.Opinion ?? "自動核保通過。"),
            "rejected" => (PolicyLifecycleService.Statuses.Rejected, "underwriting_rejected", underwritingCase.Opinion ?? "人工核保退件。"),
            "auto_rejected" => (PolicyLifecycleService.Statuses.Rejected, "underwriting_auto_rejected", underwritingCase.Opinion ?? "自動核保拒保。"),
            _ => throw new InvalidOperationException($"不支援的核保案件狀態：{underwritingCase.Status}。")
        };

        await policyLifecycleService.PrepareUnderwritingStatusAsync(
            underwritingCase.ApplicationId,
            targetStatus,
            transition,
            reason,
            cancellationToken);
    }

    private static bool ShouldPreserveManualDecision(UnderwritingCase underwritingCase) =>
        underwritingCase.Status is "approved" or "rejected" ||
        (underwritingCase.Status == "pending_documents" && underwritingCase.Decision == "request_documents");
}

public sealed class UnderwritingQueueValidationException(string message) : Exception(message);

public sealed class UnderwritingQueueConflictException(string message) : Exception(message);

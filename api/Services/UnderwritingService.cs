using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class UnderwritingService(
    IUnderwritingRepository repository,
    IAuditLogService auditLogService) : IUnderwritingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedActions = new(StringComparer.Ordinal)
    {
        "reject",
        "manual_review",
        "block_issue"
    };

    private static readonly HashSet<string> SupportedConditionTypes = new(StringComparer.Ordinal)
    {
        "vehicle_age_years_gt",
        "claim_count_last_3_years_gt",
        "document_missing"
    };

    public async Task<IReadOnlyList<UnderwritingRuleDto>> GetRulesAsync(CancellationToken cancellationToken) =>
        (await repository.GetRulesAsync(cancellationToken)).Select(MapRule).ToList();

    public async Task<UnderwritingRuleDto?> GetRuleAsync(int id, CancellationToken cancellationToken)
    {
        var rule = await repository.GetRuleByIdAsync(id, cancellationToken);
        return rule is null ? null : MapRule(rule);
    }

    public async Task<UnderwritingRuleDto> CreateRuleAsync(
        UnderwritingRuleCreateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeAndValidate(request);
        if (await repository.RuleCodeExistsAsync(normalized.Code, null, cancellationToken))
        {
            throw new UnderwritingConflictException("Code 不可重複。");
        }

        var actor = RequireAuditActor(auditActor);
        var now = DateTime.UtcNow;
        var rule = new UnderwritingRule
        {
            Code = normalized.Code,
            Name = normalized.Name,
            ConditionType = normalized.ConditionType,
            ConditionJson = normalized.ConditionJson,
            Action = normalized.Action,
            Message = normalized.Message,
            IsEnabled = normalized.IsEnabled,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddRuleAsync(rule, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "underwriting.rule.created",
            rule.Id,
            null,
            SerializeSnapshot(rule),
            cancellationToken);
        return MapRule(rule);
    }

    public async Task<UnderwritingRuleDto?> UpdateRuleAsync(
        int id,
        UnderwritingRuleUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var rule = await repository.GetRuleByIdAsync(id, cancellationToken);
        if (rule is null)
        {
            return null;
        }

        var normalized = NormalizeAndValidate(request);
        if (await repository.RuleCodeExistsAsync(normalized.Code, id, cancellationToken))
        {
            throw new UnderwritingConflictException("Code 不可重複。");
        }

        var changes = GetChanges(rule, normalized);
        if (changes.Before.Count == 0)
        {
            return MapRule(rule);
        }

        var actor = RequireAuditActor(auditActor);
        rule.Code = normalized.Code;
        rule.Name = normalized.Name;
        rule.ConditionType = normalized.ConditionType;
        rule.ConditionJson = normalized.ConditionJson;
        rule.Action = normalized.Action;
        rule.Message = normalized.Message;
        rule.IsEnabled = normalized.IsEnabled;
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        var auditAction = changes.Before.Count == 1 && changes.Before.ContainsKey("isEnabled")
            ? "underwriting.rule.enabled.changed"
            : "underwriting.rule.updated";
        await WriteAuditAsync(
            actor,
            auditAction,
            rule.Id,
            JsonSerializer.Serialize(changes.Before, JsonOptions),
            JsonSerializer.Serialize(changes.After, JsonOptions),
            cancellationToken);
        return MapRule(rule);
    }

    public async Task<bool> DeleteRuleAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var rule = await repository.GetRuleByIdAsync(id, cancellationToken);
        if (rule is null)
        {
            return false;
        }

        if (await repository.RuleHasResultsAsync(id, cancellationToken))
        {
            throw new UnderwritingConflictException("此規則已有核保評估紀錄，不可刪除；請改為停用以保留歷史一致性。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeSnapshot(rule);
        repository.RemoveRule(rule);
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "underwriting.rule.deleted",
            id,
            beforeJson,
            null,
            cancellationToken);
        return true;
    }

    public async Task<UnderwritingEvaluationDto?> EvaluateAsync(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var application = await repository.GetApplicationForEvaluationAsync(applicationId, cancellationToken);
        if (application is null)
        {
            return null;
        }

        var rules = await repository.GetEnabledRulesAsync(cancellationToken);
        var existing = await repository.GetResultsForUpdateAsync(applicationId, cancellationToken);
        var existingByRuleId = existing.ToDictionary(x => x.RuleId);
        var enabledRuleIds = rules.Select(x => x.Id).ToHashSet();
        var stale = existing.Where(x => !enabledRuleIds.Contains(x.RuleId)).ToList();
        if (stale.Count > 0)
        {
            repository.RemoveResults(stale);
        }

        var now = DateTime.UtcNow;
        foreach (var rule in rules)
        {
            var outcome = EvaluateRule(rule, application, now);
            if (!existingByRuleId.TryGetValue(rule.Id, out var result))
            {
                result = new UnderwritingResult
                {
                    ApplicationId = applicationId,
                    RuleId = rule.Id
                };
                await repository.AddResultAsync(result, cancellationToken);
                existingByRuleId[rule.Id] = result;
            }

            result.RuleCode = rule.Code;
            result.RuleName = rule.Name;
            result.Action = rule.Action;
            result.Status = outcome.Status;
            result.Message = rule.Message;
            result.Detail = outcome.Detail;
            result.EvaluatedAtUtc = now;
        }

        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetResultsAsync(applicationId, cancellationToken);
        return Aggregate(applicationId, saved);
    }

    public async Task<UnderwritingEvaluationDto?> GetResultsAsync(
        int applicationId,
        CancellationToken cancellationToken)
    {
        if (!await repository.ApplicationExistsAsync(applicationId, cancellationToken))
        {
            return null;
        }

        var results = await repository.GetResultsAsync(applicationId, cancellationToken);
        return Aggregate(applicationId, results);
    }

    private static RuleOutcome EvaluateRule(
        UnderwritingRule rule,
        InsuranceApplication application,
        DateTime evaluatedAtUtc)
    {
        try
        {
            return rule.ConditionType switch
            {
                "vehicle_age_years_gt" => EvaluateVehicleAge(rule.ConditionJson, application, evaluatedAtUtc),
                "claim_count_last_3_years_gt" => EvaluateClaimCount(rule.ConditionJson, application),
                "document_missing" => EvaluateDocumentMissing(rule.ConditionJson, application),
                _ => new RuleOutcome("unavailable", $"不支援的條件型別：{rule.ConditionType}。")
            };
        }
        catch (JsonException ex)
        {
            return new RuleOutcome("unavailable", $"條件 JSON 無法解析：{ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            return new RuleOutcome("unavailable", ex.Message);
        }
    }

    private static RuleOutcome EvaluateVehicleAge(
        string conditionJson,
        InsuranceApplication application,
        DateTime evaluatedAtUtc)
    {
        var condition = JsonSerializer.Deserialize<VehicleAgeCondition>(conditionJson, JsonOptions)
            ?? throw new InvalidDataException("車齡條件無法解析。");
        var manufactureDate = application.Quote.Vehicle.ManufactureDate;
        if (!manufactureDate.HasValue)
        {
            return new RuleOutcome("unavailable", "車輛缺少出廠日期，無法評估車齡。");
        }

        var manufacture = manufactureDate.Value.Date;
        var evaluationDate = evaluatedAtUtc.Date;
        var age = evaluationDate.Year - manufacture.Year;
        if (manufacture.AddYears(age) > evaluationDate)
        {
            age--;
        }

        var hit = age > condition.Years;
        return new RuleOutcome(
            hit ? "hit" : "passed",
            $"車齡 {age} 年，條件為大於 {condition.Years} 年。" );
    }

    private static RuleOutcome EvaluateClaimCount(
        string conditionJson,
        InsuranceApplication application)
    {
        var condition = JsonSerializer.Deserialize<ClaimCountCondition>(conditionJson, JsonOptions)
            ?? throw new InvalidDataException("理賠次數條件無法解析。");
        var premiumRequest = JsonSerializer.Deserialize<PremiumCalculationRequestDto>(
            application.Quote.PremiumRequestJson,
            JsonOptions);
        if (premiumRequest is null)
        {
            return new RuleOutcome("unavailable", "報價缺少可解析的保費試算快照，無法取得近 3 年理賠次數。");
        }

        var count = premiumRequest.ClaimCountLast3Years;
        var hit = count > condition.Count;
        return new RuleOutcome(
            hit ? "hit" : "passed",
            $"近 3 年理賠 {count} 次，條件為大於 {condition.Count} 次。" );
    }

    private static RuleOutcome EvaluateDocumentMissing(
        string conditionJson,
        InsuranceApplication application)
    {
        var condition = JsonSerializer.Deserialize<DocumentMissingCondition>(conditionJson, JsonOptions)
            ?? throw new InvalidDataException("文件條件無法解析。");
        var document = application.Documents.FirstOrDefault(
            x => string.Equals(x.DocumentType, condition.DocumentType, StringComparison.Ordinal));
        var hit = document is null || !string.Equals(document.Status, "uploaded", StringComparison.OrdinalIgnoreCase);
        var detail = document is null
            ? $"文件 {condition.DocumentType} 尚未上傳。"
            : $"文件 {condition.DocumentType} 目前狀態為 {document.Status}。";
        return new RuleOutcome(hit ? "hit" : "passed", detail);
    }

    private static NormalizedRule NormalizeAndValidate(UnderwritingRuleCreateDto request)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        var conditionType = request.ConditionType.Trim().ToLowerInvariant();
        var action = request.Action.Trim().ToLowerInvariant();
        var message = request.Message.Trim();
        var conditionJson = request.ConditionJson.Trim();

        if (code.Length == 0 || name.Length == 0 || message.Length == 0)
        {
            throw new UnderwritingValidationException("code、name、message 不可為空白。");
        }
        if (!SupportedConditionTypes.Contains(conditionType))
        {
            throw new UnderwritingValidationException(
                $"conditionType 只接受：{string.Join(", ", SupportedConditionTypes.OrderBy(x => x))}。");
        }
        if (!SupportedActions.Contains(action))
        {
            throw new UnderwritingValidationException(
                $"action 只接受：{string.Join(", ", SupportedActions.OrderBy(x => x))}。");
        }

        ValidateConditionJson(conditionType, conditionJson);
        return new NormalizedRule(code, name, conditionType, conditionJson, action, message, request.IsEnabled);
    }

    private static void ValidateConditionJson(string conditionType, string conditionJson)
    {
        try
        {
            switch (conditionType)
            {
                case "vehicle_age_years_gt":
                {
                    var condition = JsonSerializer.Deserialize<VehicleAgeCondition>(conditionJson, JsonOptions)
                        ?? throw new UnderwritingValidationException("conditionJson 無法解析。");
                    if (condition.Years < 0 || condition.Years > 100)
                    {
                        throw new UnderwritingValidationException("vehicle_age_years_gt.years 必須介於 0 到 100。");
                    }
                    break;
                }
                case "claim_count_last_3_years_gt":
                {
                    var condition = JsonSerializer.Deserialize<ClaimCountCondition>(conditionJson, JsonOptions)
                        ?? throw new UnderwritingValidationException("conditionJson 無法解析。");
                    if (condition.Count < 0 || condition.Count > 1000)
                    {
                        throw new UnderwritingValidationException("claim_count_last_3_years_gt.count 必須介於 0 到 1000。");
                    }
                    break;
                }
                case "document_missing":
                {
                    var condition = JsonSerializer.Deserialize<DocumentMissingCondition>(conditionJson, JsonOptions)
                        ?? throw new UnderwritingValidationException("conditionJson 無法解析。");
                    if (string.IsNullOrWhiteSpace(condition.DocumentType) || condition.DocumentType.Length > 50)
                    {
                        throw new UnderwritingValidationException("document_missing.documentType 必填且最長 50 字元。");
                    }
                    break;
                }
            }
        }
        catch (JsonException ex)
        {
            throw new UnderwritingValidationException($"conditionJson 不是有效 JSON：{ex.Message}");
        }
    }

    private static UnderwritingEvaluationDto Aggregate(
        int applicationId,
        IReadOnlyList<UnderwritingResult> results)
    {
        var hitActions = results
            .Where(x => string.Equals(x.Status, "hit", StringComparison.Ordinal))
            .Select(x => x.Action)
            .ToHashSet(StringComparer.Ordinal);

        var hasReject = hitActions.Contains("reject");
        var requiresManualReview = hitActions.Contains("manual_review");
        var blocksIssue = hitActions.Contains("block_issue");
        var overallAction = hasReject
            ? "reject"
            : requiresManualReview
                ? "manual_review"
                : blocksIssue
                    ? "block_issue"
                    : "approve";

        return new UnderwritingEvaluationDto(
            applicationId,
            overallAction,
            hasReject,
            requiresManualReview,
            blocksIssue,
            results.Select(MapResult).ToList());
    }

    private static RuleChanges GetChanges(UnderwritingRule rule, NormalizedRule normalized)
    {
        var before = new Dictionary<string, object?>(StringComparer.Ordinal);
        var after = new Dictionary<string, object?>(StringComparer.Ordinal);

        AddChange(before, after, "code", rule.Code, normalized.Code);
        AddChange(before, after, "name", rule.Name, normalized.Name);
        AddChange(before, after, "conditionType", rule.ConditionType, normalized.ConditionType);
        AddChange(before, after, "conditionJson", rule.ConditionJson, normalized.ConditionJson);
        AddChange(before, after, "action", rule.Action, normalized.Action);
        AddChange(before, after, "message", rule.Message, normalized.Message);
        AddChange(before, after, "isEnabled", rule.IsEnabled, normalized.IsEnabled);

        return new RuleChanges(before, after);
    }

    private static void AddChange<T>(
        IDictionary<string, object?> before,
        IDictionary<string, object?> after,
        string name,
        T oldValue,
        T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        before[name] = oldValue;
        after[name] = newValue;
    }

    private static string SerializeSnapshot(UnderwritingRule rule) =>
        JsonSerializer.Serialize(
            new
            {
                rule.Code,
                rule.Name,
                rule.ConditionType,
                rule.ConditionJson,
                rule.Action,
                rule.Message,
                rule.IsEnabled
            },
            JsonOptions);

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
        int ruleId,
        string? beforeJson,
        string? afterJson,
        CancellationToken cancellationToken) =>
        auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                action,
                "UnderwritingRule",
                ruleId.ToString(),
                beforeJson,
                afterJson,
                actor.SourceIp),
            cancellationToken);

    private static UnderwritingRuleDto MapRule(UnderwritingRule rule) =>
        new(
            rule.Id,
            rule.Code,
            rule.Name,
            rule.ConditionType,
            rule.ConditionJson,
            rule.Action,
            rule.Message,
            rule.IsEnabled,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);

    private static UnderwritingResultDto MapResult(UnderwritingResult result) =>
        new(
            result.Id,
            result.ApplicationId,
            result.RuleId,
            result.RuleCode,
            result.RuleName,
            result.Action,
            result.Status,
            result.Message,
            result.Detail,
            result.EvaluatedAtUtc);

    private sealed record VehicleAgeCondition(int Years);
    private sealed record ClaimCountCondition(int Count);
    private sealed record DocumentMissingCondition(string DocumentType);
    private sealed record RuleOutcome(string Status, string Detail);
    private sealed record RuleChanges(
        Dictionary<string, object?> Before,
        Dictionary<string, object?> After);
    private sealed record NormalizedRule(
        string Code,
        string Name,
        string ConditionType,
        string ConditionJson,
        string Action,
        string Message,
        bool IsEnabled);
}

public sealed class UnderwritingValidationException(string message) : Exception(message);

public sealed class UnderwritingConflictException(string message) : Exception(message);

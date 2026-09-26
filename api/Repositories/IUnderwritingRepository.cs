using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IUnderwritingRepository
{
    Task<IReadOnlyList<UnderwritingRule>> GetRulesAsync(CancellationToken cancellationToken);
    Task<UnderwritingRule?> GetRuleByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<UnderwritingRule>> GetEnabledRulesAsync(CancellationToken cancellationToken);
    Task<bool> RuleCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken);
    Task<bool> RuleHasResultsAsync(int id, CancellationToken cancellationToken);
    Task AddRuleAsync(UnderwritingRule rule, CancellationToken cancellationToken);
    void RemoveRule(UnderwritingRule rule);

    Task<InsuranceApplication?> GetApplicationForEvaluationAsync(int applicationId, CancellationToken cancellationToken);
    Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<UnderwritingResult>> GetResultsAsync(int applicationId, CancellationToken cancellationToken);
    Task<List<UnderwritingResult>> GetResultsForUpdateAsync(int applicationId, CancellationToken cancellationToken);
    Task AddResultAsync(UnderwritingResult result, CancellationToken cancellationToken);
    void RemoveResults(IEnumerable<UnderwritingResult> results);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

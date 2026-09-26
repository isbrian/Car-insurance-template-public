using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class UnderwritingRepository(CarInsuranceDbContext dbContext) : IUnderwritingRepository
{
    public async Task<IReadOnlyList<UnderwritingRule>> GetRulesAsync(CancellationToken cancellationToken) =>
        await dbContext.UnderwritingRules
            .AsNoTracking()
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public Task<UnderwritingRule?> GetRuleByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.UnderwritingRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<UnderwritingRule>> GetEnabledRulesAsync(CancellationToken cancellationToken) =>
        await dbContext.UnderwritingRules
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public Task<bool> RuleCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken) =>
        dbContext.UnderwritingRules.AnyAsync(
            x => x.Code == code && (!excludingId.HasValue || x.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> RuleHasResultsAsync(int id, CancellationToken cancellationToken) =>
        dbContext.UnderwritingResults.AnyAsync(x => x.RuleId == id, cancellationToken);

    public Task AddRuleAsync(UnderwritingRule rule, CancellationToken cancellationToken) =>
        dbContext.UnderwritingRules.AddAsync(rule, cancellationToken).AsTask();

    public void RemoveRule(UnderwritingRule rule) => dbContext.UnderwritingRules.Remove(rule);

    public Task<InsuranceApplication?> GetApplicationForEvaluationAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.Applications
            .AsNoTracking()
            .AsSplitQuery()
            .Include(x => x.Quote)
                .ThenInclude(x => x.Vehicle)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

    public Task<bool> ApplicationExistsAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.Applications.AnyAsync(x => x.Id == applicationId, cancellationToken);

    public async Task<IReadOnlyList<UnderwritingResult>> GetResultsAsync(int applicationId, CancellationToken cancellationToken) =>
        await dbContext.UnderwritingResults
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.RuleCode)
            .ToListAsync(cancellationToken);

    public Task<List<UnderwritingResult>> GetResultsForUpdateAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.UnderwritingResults
            .Where(x => x.ApplicationId == applicationId)
            .ToListAsync(cancellationToken);

    public Task AddResultAsync(UnderwritingResult result, CancellationToken cancellationToken) =>
        dbContext.UnderwritingResults.AddAsync(result, cancellationToken).AsTask();

    public void RemoveResults(IEnumerable<UnderwritingResult> results) =>
        dbContext.UnderwritingResults.RemoveRange(results);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

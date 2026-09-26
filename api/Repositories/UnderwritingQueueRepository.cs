using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class UnderwritingQueueRepository(CarInsuranceDbContext dbContext) : IUnderwritingQueueRepository
{
    private IQueryable<UnderwritingCase> ReadQuery() => dbContext.UnderwritingCases
        .AsNoTracking()
        .Include(x => x.Application)
            .ThenInclude(x => x.Quote)
                .ThenInclude(x => x.Vehicle);

    public async Task<IReadOnlyList<InsuranceApplication>> GetSubmittedApplicationsAsync(CancellationToken cancellationToken) =>
        await dbContext.Applications
            .AsNoTracking()
            .Include(x => x.Quote)
                .ThenInclude(x => x.Vehicle)
            .Where(x => x.Status == "submitted")
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<UnderwritingCase?> GetByApplicationIdForUpdateAsync(
        int applicationId,
        CancellationToken cancellationToken) =>
        dbContext.UnderwritingCases
            .Include(x => x.Application)
                .ThenInclude(x => x.Quote)
                    .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);

    public Task<UnderwritingCase?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<UnderwritingCase?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.UnderwritingCases
            .Include(x => x.Application)
                .ThenInclude(x => x.Quote)
                    .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<UnderwritingCase> Items, int Total)> SearchAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ReadQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var trimmed = keyword.Trim();
            query = query.Where(x =>
                x.Application.ApplicationNumber.Contains(trimmed) ||
                (x.Application.InsuredName != null && x.Application.InsuredName.Contains(trimmed)) ||
                x.Application.Quote.Vehicle.LicensePlate.Contains(trimmed));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken) =>
        await dbContext.UnderwritingCases
            .AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

    public Task AddAsync(UnderwritingCase underwritingCase, CancellationToken cancellationToken) =>
        dbContext.UnderwritingCases.AddAsync(underwritingCase, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

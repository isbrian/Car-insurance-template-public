using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IUnderwritingQueueRepository
{
    Task<IReadOnlyList<InsuranceApplication>> GetSubmittedApplicationsAsync(CancellationToken cancellationToken);
    Task<UnderwritingCase?> GetByApplicationIdForUpdateAsync(int applicationId, CancellationToken cancellationToken);
    Task<UnderwritingCase?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UnderwritingCase?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<UnderwritingCase> Items, int Total)> SearchAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<Dictionary<string, int>> GetStatusCountsAsync(CancellationToken cancellationToken);
    Task AddAsync(UnderwritingCase underwritingCase, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

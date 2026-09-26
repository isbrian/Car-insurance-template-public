using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IUserManagementRepository
{
    Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken);
    Task<UserAccount?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UserAccount?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

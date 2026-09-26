using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IAuthRepository
{
    Task<UserAccount?> GetByAccountAsync(string account, CancellationToken cancellationToken);
    Task<UserAccount?> GetByAccountForUpdateAsync(string account, CancellationToken cancellationToken);
    Task<UserAccount?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<UserAccount?> GetByResetTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

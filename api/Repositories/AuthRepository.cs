using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class AuthRepository(CarInsuranceDbContext dbContext) : IAuthRepository
{
    public Task<UserAccount?> GetByAccountAsync(string account, CancellationToken cancellationToken)
    {
        var normalized = account.ToLower();
        return dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Username.ToLower() == normalized || x.Email.ToLower() == normalized,
                cancellationToken);
    }

    public Task<UserAccount?> GetByAccountForUpdateAsync(string account, CancellationToken cancellationToken)
    {
        var normalized = account.ToLower();
        return dbContext.UserAccounts
            .FirstOrDefaultAsync(
                x => x.Username.ToLower() == normalized || x.Email.ToLower() == normalized,
                cancellationToken);
    }

    public Task<UserAccount?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<UserAccount?> GetByResetTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .FirstOrDefaultAsync(x => x.PasswordResetTokenHash == tokenHash, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

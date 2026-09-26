using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class UserManagementRepository(CarInsuranceDbContext dbContext) : IUserManagementRepository
{
    public async Task<IReadOnlyList<UserAccount>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.UserAccounts
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<UserAccount?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<UserAccount?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

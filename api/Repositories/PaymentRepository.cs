using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class PaymentRepository(CarInsuranceDbContext dbContext) : IPaymentRepository
{
    private IQueryable<Payment> ReadQuery() => dbContext.Payments
        .AsNoTracking()
        .Include(x => x.Application)
            .ThenInclude(x => x.Quote)
                .ThenInclude(x => x.Customer)
        .Include(x => x.Application)
            .ThenInclude(x => x.Quote)
                .ThenInclude(x => x.Vehicle);

    private IQueryable<Payment> UpdateQuery() => dbContext.Payments
        .Include(x => x.Application)
            .ThenInclude(x => x.Quote)
                .ThenInclude(x => x.Customer)
        .Include(x => x.Application)
            .ThenInclude(x => x.Quote)
                .ThenInclude(x => x.Vehicle);

    public async Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken) =>
        await ReadQuery()
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Payment?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);

    public Task<Payment?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        UpdateQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InsuranceApplication?> GetApplicationAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.Applications
            .AsNoTracking()
            .Include(x => x.Quote)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Quote)
                .ThenInclude(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

    public Task<bool> ExistsForApplicationAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.Payments.AnyAsync(x => x.ApplicationId == applicationId, cancellationToken);

    public Task<string?> GetLatestPaymentNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Payments
            .AsNoTracking()
            .Where(x => x.PaymentNumber.StartsWith(prefix))
            .OrderByDescending(x => x.PaymentNumber)
            .Select(x => x.PaymentNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Payment>> GetPendingDueBeforeForUpdateAsync(
        DateTime nowUtc,
        CancellationToken cancellationToken) =>
        await UpdateQuery()
            .Where(x => x.Status == "pending" && x.DueAtUtc < nowUtc)
            .OrderBy(x => x.DueAtUtc)
            .ToListAsync(cancellationToken);

    public Task<string?> GetLifecycleStatusAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.PolicyLifecycles
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Payment payment, CancellationToken cancellationToken) =>
        dbContext.Payments.AddAsync(payment, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

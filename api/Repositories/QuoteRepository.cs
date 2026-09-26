using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class QuoteRepository(CarInsuranceDbContext dbContext) : IQuoteRepository
{
    public async Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Quotes
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<Quote?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Quotes
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Quote?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Quotes
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(x => x.Id == customerId, cancellationToken);

    public Task<int?> GetVehicleCustomerIdAsync(int vehicleId, CancellationToken cancellationToken) =>
        dbContext.Vehicles
            .Where(x => x.Id == vehicleId)
            .Select(x => (int?)x.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<string?> GetLatestQuoteNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Quotes
            .AsNoTracking()
            .Where(x => x.QuoteNumber.StartsWith(prefix))
            .OrderByDescending(x => x.QuoteNumber)
            .Select(x => x.QuoteNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Quote quote, CancellationToken cancellationToken) =>
        dbContext.Quotes.AddAsync(quote, cancellationToken).AsTask();

    public Task RemoveAsync(Quote quote, CancellationToken cancellationToken)
    {
        dbContext.Quotes.Remove(quote);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

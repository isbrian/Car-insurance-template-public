using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class CustomerRepository(CarInsuranceDbContext dbContext) : ICustomerRepository
{
    public async Task<(IReadOnlyList<Customer> Items, int Total)> SearchAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Customers
            .AsNoTracking()
            .Include(customer => customer.Vehicles)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(customer =>
                customer.Name.Contains(normalizedKeyword) ||
                customer.NationalId.Contains(normalizedKeyword) ||
                customer.Phone.Contains(normalizedKeyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(customer => customer.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        return dbContext.Customers
            .Include(customer => customer.Vehicles)
            .SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByNationalIdAsync(
        string nationalId,
        int? excludingId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Customers.Where(customer => customer.NationalId == nationalId);
        if (excludingId.HasValue)
        {
            query = query.Where(customer => customer.Id != excludingId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}

using CarInsurance.Api.Data;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.InfrastructureTests;

public sealed class DatabaseIsolationTests
{
    [Fact]
    public async Task ResetDatabase_RecreatesDeterministicSeed_WithoutUsingDevelopmentDatabase()
    {
        using var factory = new CarInsuranceApiFactory();
        _ = factory.CreateClient();

        Assert.False(
            Path.GetFullPath(factory.DatabasePath).Equals(
                Path.GetFullPath(Path.Combine("api", "carinsurance.db")),
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains("car-insurance-api-tests", factory.DatabasePath, StringComparison.Ordinal);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            Assert.Equal(3, await db.Customers.CountAsync());
            Assert.Equal(5, await db.UserAccounts.CountAsync());

            var firstCustomer = await db.Customers.OrderBy(x => x.Id).FirstAsync();
            firstCustomer.Name = "測試污染資料";
            await db.SaveChangesAsync();
            Assert.Equal("測試污染資料", (await db.Customers.OrderBy(x => x.Id).FirstAsync()).Name);
        }

        await factory.ResetDatabaseAsync();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            Assert.Equal(3, await db.Customers.CountAsync());
            Assert.Equal(5, await db.UserAccounts.CountAsync());
            Assert.Equal("王小明", (await db.Customers.OrderBy(x => x.Id).FirstAsync()).Name);
            Assert.Equal(
                TestAuth.DemoRoles.OrderBy(x => x.Key).Select(x => x.Key),
                await db.UserAccounts.OrderBy(x => x.Username).Select(x => x.Username).ToArrayAsync());
        }
    }
}

using CarInsurance.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.Infrastructure;

public sealed class CarInsuranceApiFactory : WebApplicationFactory<Program>
{
    public const string DemoPassword = "Demo123!";
    public const string MfaCode = "123456";

    private readonly string _tempDirectory;
    private readonly Dictionary<string, string?> _previousEnvironmentValues = new(StringComparer.Ordinal);

    public CarInsuranceApiFactory()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "car-insurance-api-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        SetEnvironment("ASPNETCORE_ENVIRONMENT", "Testing");
        SetEnvironment("ConnectionStrings__Default", $"Data Source={DatabasePath}");
        SetEnvironment("DocumentStorage__Root", Path.Combine(_tempDirectory, "uploads"));
        SetEnvironment("PolicyIssuance__StorageRoot", Path.Combine(_tempDirectory, "policies"));
        SetEnvironment("Jwt__Issuer", "CarInsurance.Api.Tests");
        SetEnvironment("Jwt__Audience", "CarInsurance.Api.Tests.Client");
        SetEnvironment("Jwt__SigningKey", "car-insurance-tests-signing-key-2026-at-least-32-bytes");
        SetEnvironment("Jwt__AccessTokenMinutes", "60");
        SetEnvironment("Jwt__MfaTokenMinutes", "5");
        SetEnvironment("Auth__DemoPassword", DemoPassword);
        SetEnvironment("Auth__MfaCode", MfaCode);
        SetEnvironment("RenewalReminder__Enabled", "false");
    }

    public string DatabasePath => Path.Combine(_tempDirectory, "carinsurance-tests.db");

    public async Task ResetDatabaseAsync()
    {
        _ = Server;

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        await SeedData.InitializeAsync(dbContext, configuration);
    }

    private void SetEnvironment(string name, string value)
    {
        _previousEnvironmentValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        foreach (var (name, value) in _previousEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(name, value);
        }

        if (!Directory.Exists(_tempDirectory))
        {
            return;
        }

        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Test runner/SQLite 可能仍短暫持有檔案；OS temp directory 會負責後續清理。
        }
        catch (UnauthorizedAccessException)
        {
            // 同上，避免清理時機影響測試結果。
        }
    }
}

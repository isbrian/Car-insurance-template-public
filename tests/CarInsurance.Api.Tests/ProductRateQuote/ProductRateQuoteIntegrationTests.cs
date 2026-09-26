using System.Net;
using System.Net.Http.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.ProductRateQuote;

public sealed class ProductRateQuoteIntegrationTests
{
    [Fact]
    public async Task InsuranceTypes_List_ReturnsSeededTypes()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/insurance-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<InsuranceTypeDto>>();
        Assert.NotNull(items);
        Assert.Contains(items, x => x.Code == "CTP");
        Assert.Contains(items, x => x.Code == "TPL");
        Assert.Contains(items, x => x.Code == "BODY");
    }

    [Fact]
    public async Task InsuranceTypes_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/insurance-types/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InsuranceTypes_CreateUpdateDelete_WorksForUnusedType()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/insurance-types", new InsuranceTypeCreateDto
        {
            Code = "TEST-TYPE",
            Name = "整合測試險種",
            Description = "建立更新刪除測試",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InsuranceTypeDto>();
        Assert.NotNull(created);

        var updateResponse = await client.PutAsJsonAsync($"/api/insurance-types/{created.Id}", new InsuranceTypeUpdateDto
        {
            Code = "TEST-TYPE",
            Name = "整合測試險種更新",
            Description = "updated",
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<InsuranceTypeDto>();
        Assert.NotNull(updated);
        Assert.Equal("整合測試險種更新", updated.Name);
        Assert.False(updated.IsActive);

        var deleteResponse = await client.DeleteAsync($"/api/insurance-types/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task InsuranceTypes_CreateDuplicateCode_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/insurance-types", new InsuranceTypeCreateDto
        {
            Code = "CTP",
            Name = "duplicate"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InsuranceTypes_CreateMissingRequiredName_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/insurance-types", new InsuranceTypeCreateDto
        {
            Code = "BAD-TYPE",
            Name = string.Empty
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task InsuranceTypes_DeleteSeedTypeWithProducts_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var typeId = await GetInsuranceTypeIdAsync(factory, "CTP");

        var response = await client.DeleteAsync($"/api/insurance-types/{typeId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InsuranceProducts_ListFilterAndValidation_Work()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var ctpTypeId = await GetInsuranceTypeIdAsync(factory, "CTP");

        var response = await client.GetAsync($"/api/insurance-products?insuranceTypeId={ctpTypeId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<InsuranceProductDto>>();
        Assert.NotNull(items);
        Assert.Contains(items, x => x.Code == "CTP-AUTO");
        Assert.All(items, x => Assert.Equal(ctpTypeId, x.InsuranceTypeId));

        var invalidResponse = await client.GetAsync("/api/insurance-products?insuranceTypeId=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task InsuranceProducts_CreateAndUpdate_Works()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var typeId = await GetInsuranceTypeIdAsync(factory, "CTP");

        var createResponse = await client.PostAsJsonAsync("/api/insurance-products", new InsuranceProductCreateDto
        {
            InsuranceTypeId = typeId,
            Code = "TEST-PRODUCT",
            Name = "整合測試商品",
            Description = "integration",
            IsActive = true
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InsuranceProductDto>();
        Assert.NotNull(created);

        var updateResponse = await client.PutAsJsonAsync($"/api/insurance-products/{created.Id}", new InsuranceProductUpdateDto
        {
            InsuranceTypeId = typeId,
            Code = "TEST-PRODUCT",
            Name = "整合測試商品更新",
            Description = "updated",
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<InsuranceProductDto>();
        Assert.NotNull(updated);
        Assert.Equal("整合測試商品更新", updated.Name);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task InsuranceProducts_CreateMissingType_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/insurance-products", new InsuranceProductCreateDto
        {
            InsuranceTypeId = 999999,
            Code = "MISSING-TYPE",
            Name = "missing"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InsuranceProducts_CreateDuplicateCode_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var typeId = await GetInsuranceTypeIdAsync(factory, "CTP");

        var response = await client.PostAsJsonAsync("/api/insurance-products", new InsuranceProductCreateDto
        {
            InsuranceTypeId = typeId,
            Code = "CTP-AUTO",
            Name = "duplicate"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task InsuranceProducts_DeleteSeedProductWithRateVersions_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var productId = await GetInsuranceProductIdAsync(factory, "CTP-AUTO");

        var response = await client.DeleteAsync($"/api/insurance-products/{productId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RateVersions_ListFilterAndValidation_Work()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var productId = await GetInsuranceProductIdAsync(factory, "CTP-AUTO");

        var response = await client.GetAsync($"/api/rate-versions?insuranceProductId={productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await response.Content.ReadFromJsonAsync<List<RateVersionDto>>();
        Assert.NotNull(items);
        Assert.Contains(items, x => x.Version == "provisional-v0.1");
        Assert.All(items, x => Assert.Equal(productId, x.InsuranceProductId));

        var invalidResponse = await client.GetAsync("/api/rate-versions?insuranceProductId=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
    }

    [Fact]
    public async Task RateVersions_Create_ReturnsCreated()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var productId = await GetInsuranceProductIdAsync(factory, "CTP-AUTO");

        var response = await client.PostAsJsonAsync("/api/rate-versions", new RateVersionCreateDto
        {
            InsuranceProductId = productId,
            Version = "integration-v1",
            EffectiveFrom = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Confidence = "estimated",
            IsActive = false
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RateVersionDto>();
        Assert.NotNull(created);
        Assert.Equal("integration-v1", created.Version);
        Assert.Equal("estimated", created.Confidence);
    }

    [Fact]
    public async Task RateVersions_InvalidPeriod_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var productId = await GetInsuranceProductIdAsync(factory, "CTP-AUTO");

        var response = await client.PostAsJsonAsync("/api/rate-versions", new RateVersionCreateDto
        {
            InsuranceProductId = productId,
            Version = "bad-period",
            EffectiveFrom = new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo = new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            Confidence = "estimated"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RateVersions_InvalidConfidence_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var productId = await GetInsuranceProductIdAsync(factory, "CTP-AUTO");

        var response = await client.PostAsJsonAsync("/api/rate-versions", new RateVersionCreateDto
        {
            InsuranceProductId = productId,
            Version = "bad-confidence",
            EffectiveFrom = DateTime.UtcNow,
            Confidence = "unknown"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RateVersions_Duplicate_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var productId = await GetInsuranceProductIdAsync(factory, "CTP-AUTO");

        var response = await client.PostAsJsonAsync("/api/rate-versions", new RateVersionCreateDto
        {
            InsuranceProductId = productId,
            Version = "provisional-v0.1",
            EffectiveFrom = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            Confidence = "provisional"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task RateVersions_MissingProduct_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/rate-versions", new RateVersionCreateDto
        {
            InsuranceProductId = 999999,
            Version = "missing-product",
            EffectiveFrom = DateTime.UtcNow,
            Confidence = "estimated"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RateTables_GetSeedTable_ReturnsProvisionalData()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.GetAsync($"/api/rate-tables/{rateVersionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var table = await response.Content.ReadFromJsonAsync<RateTableDto>();
        Assert.NotNull(table);
        Assert.Equal(rateVersionId, table.RateVersionId);
        Assert.Equal("provisional-v0.1", table.SourceVersion);
        Assert.True(table.IsProvisional);
        Assert.False(string.IsNullOrWhiteSpace(table.DisplayWarning));
        Assert.True(table.Data.TryGetProperty("compulsory", out _));
        Assert.True(table.Data.TryGetProperty("voluntary", out _));
    }

    [Fact]
    public async Task RateTables_InvalidAndMissing_ReturnExpectedErrors()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var invalid = await client.GetAsync("/api/rate-tables/0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var missing = await client.GetAsync("/api/rate-tables/999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task PremiumCalculation_ValidSeedRate_ReturnsBreakdown()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/premium-calculations", CreateCalculation(rateVersionId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PremiumCalculationResponseDto>();
        Assert.NotNull(result);
        Assert.Equal(rateVersionId, result.RateVersionId);
        Assert.Equal("provisional-v0.1", result.SourceVersion);
        Assert.True(result.IsProvisional);
        Assert.True(result.TotalPremium > 0);
        Assert.NotEmpty(result.Breakdown);
    }

    [Fact]
    public async Task PremiumCalculation_InvalidGender_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);
        var valid = CreateCalculation(rateVersionId);
        var invalid = valid with { DriverGender = "other" };

        var response = await client.PostAsJsonAsync("/api/premium-calculations", invalid);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PremiumCalculation_MissingRateTable_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/premium-calculations", CreateCalculation(999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quotes_Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Quotes_Create_AsClaims_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "claims");
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Quotes_Create_AsSales_ReturnsCreatedPersistsAndWritesAudit()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<QuoteDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.StartsWith("QT-", created.QuoteNumber);
        Assert.Equal(1, created.CustomerId);
        Assert.Equal(1, created.VehicleId);
        Assert.Equal("draft", created.Status);
        Assert.True(created.TotalPremium > 0);
        Assert.NotEmpty(created.Breakdown);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.Quotes.AnyAsync(x => x.Id == created.Id));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "quote.created" && x.ObjectId == created.Id.ToString()));
    }

    [Fact]
    public async Task Quotes_Create_WithVehicleOwnedByAnotherCustomer_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId) with
        {
            CustomerId = 1,
            VehicleId = 2
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Quotes_Create_WithMissingCustomer_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId) with
        {
            CustomerId = 999999,
            VehicleId = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Quotes_Create_WithInvalidStatus_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var response = await client.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId) with { Status = "expired" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Quotes_ListGetUpdateDelete_FollowsCurrentContract()
    {
        using var factory = new CarInsuranceApiFactory();
        using var salesClient = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var rateVersionId = await GetSeedRateVersionIdAsync(factory);

        var createResponse = await salesClient.PostAsJsonAsync("/api/quotes", CreateQuote(rateVersionId));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<QuoteDto>();
        Assert.NotNull(created);

        using var anonymousClient = factory.CreateClient();
        var listResponse = await anonymousClient.GetAsync("/api/quotes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<List<QuoteDto>>();
        Assert.NotNull(list);
        Assert.Contains(list, x => x.Id == created.Id);

        var getResponse = await anonymousClient.GetAsync($"/api/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var update = new QuoteUpdateDto(
            created.CustomerId,
            created.VehicleId,
            GetFutureBusinessDate(2),
            "匿名更新測試",
            "integration",
            "active",
            CreateCalculation(rateVersionId));
        var updateResponse = await anonymousClient.PutAsJsonAsync($"/api/quotes/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<QuoteDto>();
        Assert.NotNull(updated);
        Assert.Equal("active", updated.Status);
        Assert.Equal("匿名更新測試", updated.SalesAgent);

        var deleteResponse = await anonymousClient.DeleteAsync($"/api/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var missingAfterDelete = await anonymousClient.GetAsync($"/api/quotes/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, missingAfterDelete.StatusCode);
    }

    private static PremiumCalculationRequestDto CreateCalculation(int rateVersionId) => new(
        rateVersionId,
        new DateOnly(1988, 5, 20),
        "male",
        4,
        0,
        "自用小客車",
        "TOYOTA-COROLLA",
        new DateOnly(2021, 6, 1),
        "自用",
        null,
        null,
        null,
        null,
        null,
        false,
        null,
        Array.Empty<string>(),
        false,
        false,
        false);

    private static QuoteCreateDto CreateQuote(int rateVersionId) => new(
        1,
        1,
        GetFutureBusinessDate(1),
        "整合測試業務",
        "integration",
        "draft",
        CreateCalculation(rateVersionId));

    private static DateOnly GetFutureBusinessDate(int days)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz)).AddDays(days);
    }

    private static async Task<int> GetInsuranceTypeIdAsync(CarInsuranceApiFactory factory, string code)
    {
        _ = factory.Server;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        return await db.InsuranceTypes.Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
    }

    private static async Task<int> GetInsuranceProductIdAsync(CarInsuranceApiFactory factory, string code)
    {
        _ = factory.Server;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        return await db.InsuranceProducts.Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
    }

    private static async Task<int> GetSeedRateVersionIdAsync(CarInsuranceApiFactory factory)
    {
        _ = factory.Server;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        return await db.RateVersions
            .Where(x => x.Version == "provisional-v0.1" && x.InsuranceProduct.Code == "CTP-AUTO")
            .Select(x => x.Id)
            .SingleAsync();
    }
}

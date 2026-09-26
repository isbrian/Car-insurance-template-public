using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.CustomersVehicles;

public sealed class CustomerVehicleIntegrationTests
{
    [Fact]
    public async Task Customers_List_ReturnsSeededMaskedCustomers()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/customers?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(3, payload.Total);
        Assert.Equal(3, payload.Items.Count);
        Assert.Equal(1, payload.Page);
        Assert.Equal(20, payload.PageSize);
        Assert.Equal("王小明", payload.Items[0].Name);
        Assert.Equal("A****89", payload.Items[0].NationalId);
        Assert.Equal("09**-***-678", payload.Items[0].Phone);
    }

    [Fact]
    public async Task Customers_GetById_ReturnsMaskedDetailsWithVehicles()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/customers/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var customer = await response.Content.ReadFromJsonAsync<CustomerDetails>();
        Assert.NotNull(customer);
        Assert.Equal("王小明", customer.Name);
        Assert.Equal("A****89", customer.NationalId);
        Assert.Equal("09**-***-678", customer.Phone);
        Assert.NotEmpty(customer.Vehicles);
        Assert.Contains(customer.Vehicles, vehicle => vehicle.LicensePlate == "ABC-1234");
    }

    [Theory]
    [InlineData("/api/customers?page=0&pageSize=20")]
    [InlineData("/api/customers?page=1&pageSize=101")]
    public async Task Customers_List_WithInvalidPaging_ReturnsBadRequest(string url)
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/customers/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customers_Create_ReturnsCreatedAndPersistsMaskedResponse()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var request = new CustomerCreateDto
        {
            Name = "測試客戶",
            NationalId = "Z987654321",
            Phone = "0987654321",
            Email = "integration@example.com",
            BirthDate = new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            Address = "台北市測試路 1 號"
        };

        var response = await client.PostAsJsonAsync("/api/customers", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal("測試客戶", created.Name);
        Assert.Equal("Z****21", created.NationalId);
        Assert.Equal("09**-***-321", created.Phone);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var stored = await db.Customers.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal("Z987654321", stored.NationalId);
        Assert.Equal("0987654321", stored.Phone);
    }

    [Fact]
    public async Task Customers_Create_WithInvalidNationalId_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/customers", new CustomerCreateDto
        {
            Name = "格式錯誤",
            NationalId = "INVALID",
            Phone = "0987654321"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_Create_WithDuplicateNationalId_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/customers", new CustomerCreateDto
        {
            Name = "重複身分證",
            NationalId = "A123456789",
            Phone = "0987654321"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Customers_Update_Existing_ReturnsUpdatedCustomer()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/customers/1", new CustomerCreateDto
        {
            Name = "王小明更新",
            NationalId = "A123456789",
            Phone = "0911222333",
            Email = "updated@example.com",
            Address = "台北市更新路 2 號"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<CustomerDto>();
        Assert.NotNull(updated);
        Assert.Equal("王小明更新", updated.Name);
        Assert.Equal("09**-***-333", updated.Phone);
        Assert.Equal("updated@example.com", updated.Email);
    }

    [Fact]
    public async Task Customers_Update_Missing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/customers/999999", new CustomerCreateDto
        {
            Name = "不存在",
            NationalId = "Z987654321",
            Phone = "0987654321"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customers_UnmaskPii_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/customers/1/pii/unmask",
            new CustomerPiiUnmaskRequestDto("integration test"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customers_UnmaskPii_WithRoleOutsidePermissionMap_ReturnsForbidden()
    {
        using var factory = new CarInsuranceApiFactory();
        _ = factory.Server;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            var sales = await db.UserAccounts.SingleAsync(x => x.Username == "sales");
            sales.Role = "測試無權限角色";
            await db.SaveChangesAsync();
        }

        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");
        var response = await client.PostAsJsonAsync(
            "/api/customers/1/pii/unmask",
            new CustomerPiiUnmaskRequestDto("integration test"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customers_UnmaskPii_WithAuthorizedRole_ReturnsFullPii()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await client.PostAsJsonAsync(
            "/api/customers/1/pii/unmask",
            new CustomerPiiUnmaskRequestDto("核對投保資料"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CustomerPiiUnmaskResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.Id);
        Assert.Equal("A123456789", payload.NationalId);
        Assert.Equal("0912345678", payload.Phone);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "customer.pii.unmasked"));
    }

    [Fact]
    public async Task Customers_UnmaskPii_WithBlankReason_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await client.PostAsJsonAsync(
            "/api/customers/1/pii/unmask",
            new CustomerPiiUnmaskRequestDto("   "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_List_ReturnsSeededVehiclesWithMaskedVin()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<VehicleListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.TotalCount);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal("ABC-1234", payload.Items[0].LicensePlate);
        Assert.NotEqual("DEMO-VIN-000000000001", payload.Items[0].Vin);
        Assert.EndsWith("0001", payload.Items[0].Vin);
    }

    [Fact]
    public async Task Vehicles_GetById_ReturnsSeededVehicle()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vehicle = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(vehicle);
        Assert.Equal("ABC-1234", vehicle.LicensePlate);
        Assert.Equal(1, vehicle.CustomerId);
        Assert.Equal("王小明", vehicle.CustomerName);
    }

    [Theory]
    [InlineData("/api/vehicles?page=0&pageSize=20")]
    [InlineData("/api/vehicles?customerId=0&page=1&pageSize=20")]
    public async Task Vehicles_List_WithInvalidQuery_ReturnsBadRequest(string url)
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_GetMissing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Create_ReturnsCreatedAndPersistsVehicle()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();
        var request = new VehicleCreateDto
        {
            LicensePlate = "xyz-8899",
            Vin = "integration-vin-00000001",
            MakeModelCode = "TESLA-MODEL3",
            ManufactureDate = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            UsageType = "自用",
            CustomerId = 1
        };

        var response = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(created);
        Assert.Equal("XYZ-8899", created.LicensePlate);
        Assert.EndsWith("0001", created.Vin);
        Assert.NotEqual("INTEGRATION-VIN-00000001", created.Vin);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var stored = await db.Vehicles.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        Assert.Equal("XYZ-8899", stored.LicensePlate);
        Assert.Equal("INTEGRATION-VIN-00000001", stored.Vin);
    }

    [Fact]
    public async Task Vehicles_Create_WithMissingCustomer_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vehicles", NewVehicle(
            licensePlate: "NEW-1001",
            vin: "VIN-MISSING-CUSTOMER-01",
            customerId: 999999));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Create_WithDuplicateLicensePlate_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vehicles", NewVehicle(
            licensePlate: "ABC-1234",
            vin: "VIN-UNIQUE-DUP-PLATE-01",
            customerId: 1));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Create_WithInvalidCustomerId_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/vehicles", NewVehicle(
            licensePlate: "NEW-1002",
            vin: "VIN-INVALID-CUSTOMER-01",
            customerId: 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Update_Existing_ReturnsUpdatedVehicle()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/vehicles/1", new VehicleUpdateDto
        {
            LicensePlate = "upd-4321",
            Vin = "updated-vin-00000001",
            MakeModelCode = "TOYOTA-COROLLA-CROSS",
            UsageType = "營業",
            CustomerId = 2
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(updated);
        Assert.Equal("UPD-4321", updated.LicensePlate);
        Assert.Equal(2, updated.CustomerId);
        Assert.Equal("陳美玲", updated.CustomerName);
        Assert.EndsWith("0001", updated.Vin);
    }

    [Fact]
    public async Task Vehicles_Update_Missing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/vehicles/999999", new VehicleUpdateDto
        {
            LicensePlate = "NEW-9999",
            Vin = "VIN-UPDATE-MISSING-0001",
            MakeModelCode = "TEST-CAR",
            UsageType = "自用",
            CustomerId = 1
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Update_WithDuplicateLicensePlate_ReturnsConflict()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/vehicles/2", new VehicleUpdateDto
        {
            LicensePlate = "ABC-1234",
            Vin = "DEMO-VIN-000000000002",
            MakeModelCode = "HONDA-CRV",
            UsageType = "自用",
            CustomerId = 2
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Delete_NewlyCreated_ReturnsNoContentThenNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/vehicles", NewVehicle(
            licensePlate: "DEL-7788",
            vin: "VIN-DELETE-INTEGRATION-01",
            customerId: 1));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<VehicleDto>();
        Assert.NotNull(created);

        var deleteResponse = await client.DeleteAsync($"/api/vehicles/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/vehicles/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Vehicles_Delete_Missing_ReturnsNotFound()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/vehicles/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static VehicleCreateDto NewVehicle(string licensePlate, string vin, int customerId) => new()
    {
        LicensePlate = licensePlate,
        Vin = vin,
        MakeModelCode = "TEST-CAR",
        UsageType = "自用",
        CustomerId = customerId
    };
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;

namespace CarInsurance.Api.Tests.Smoke;

public sealed class ApiSmokeTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", payload.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sales_LoginMfaAndMe_ReturnsSeededRole()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var token = await TestAuth.LoginAsAsync(client, "sales");
        Assert.Equal("業務員", token.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<AuthenticatedUserDto>();
        Assert.NotNull(me);
        Assert.Equal("sales", me.Username);
        Assert.Equal("業務員", me.Role);
    }

    [Fact]
    public async Task FiveDemoRoles_CanObtainAccessTokens()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        foreach (var (account, expectedRole) in TestAuth.DemoRoles)
        {
            var token = await TestAuth.LoginAsAsync(client, account);

            Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
            Assert.Equal(account, token.User.Username);
            Assert.Equal(expectedRole, token.User.Role);
        }
    }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CarInsurance.Api.Tests.Auth;

public sealed class AuthIntegrationTests
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsMfaChallenge()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", CarInsuranceApiFactory.DemoPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var challenge = await response.Content.ReadFromJsonAsync<MfaChallengeDto>();
        Assert.NotNull(challenge);
        Assert.True(challenge.RequiresMfa);
        Assert.False(string.IsNullOrWhiteSpace(challenge.MfaToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "Demo123!")]
    [InlineData("sales", "")]
    public async Task Login_WithBlankFields_ReturnsBadRequest(string account, string password)
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto(account, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MfaVerify_WithValidTokenAndCode_ReturnsAccessToken()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", CarInsuranceApiFactory.DemoPassword));
        login.EnsureSuccessStatusCode();
        var challenge = await login.Content.ReadFromJsonAsync<MfaChallengeDto>();
        Assert.NotNull(challenge);

        var response = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerifyRequestDto(challenge.MfaToken, CarInsuranceApiFactory.MfaCode));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<AuthTokenResponseDto>();
        Assert.NotNull(token);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Equal("sales", token.User.Username);
        Assert.Equal("業務員", token.User.Role);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
    }

    [Fact]
    public async Task MfaVerify_WithWrongCode_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", CarInsuranceApiFactory.DemoPassword));
        login.EnsureSuccessStatusCode();
        var challenge = await login.Content.ReadFromJsonAsync<MfaChallengeDto>();
        Assert.NotNull(challenge);

        var response = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerifyRequestDto(challenge.MfaToken, "000000"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MfaVerify_WithInvalidToken_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerifyRequestDto("not-a-valid-jwt", CarInsuranceApiFactory.MfaCode));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithAccessToken_ReturnsAuthenticatedUser()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = await TestAuth.CreateAuthenticatedClientAsync(factory, "sales");

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<AuthenticatedUserDto>();
        Assert.NotNull(me);
        Assert.Equal("sales", me.Username);
        Assert.Equal("業務員", me.Role);
    }

    [Fact]
    public async Task Me_WithoutBearerToken_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithMfaTokenAsBearer_ReturnsUnauthorized()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", CarInsuranceApiFactory.DemoPassword));
        login.EnsureSuccessStatusCode();
        var challenge = await login.Content.ReadFromJsonAsync<MfaChallengeDto>();
        Assert.NotNull(challenge);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", challenge.MfaToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_ForExistingAccount_ReturnsAcceptedAndStoresResetTokenHash()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequestDto("sales"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
        Assert.NotNull(payload);
        Assert.Null(payload.DemoResetToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
        var sales = await db.UserAccounts.AsNoTracking().SingleAsync(x => x.Username == "sales");
        Assert.False(string.IsNullOrWhiteSpace(sales.PasswordResetTokenHash));
        Assert.True(sales.PasswordResetExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownAccount_ReturnsAcceptedWithoutDisclosure()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/forgot-password",
            new ForgotPasswordRequestDto("missing-user@example.com"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ForgotPasswordResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("若帳號存在，系統已建立密碼重設請求。", payload.Message);
        Assert.Null(payload.DemoResetToken);
    }

    [Fact]
    public async Task ResetPassword_WithKnownValidToken_ChangesLoginPassword()
    {
        const string resetToken = "integration-reset-token";
        const string newPassword = "NewDemo456!";

        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CarInsuranceDbContext>();
            var sales = await db.UserAccounts.SingleAsync(x => x.Username == "sales");
            sales.PasswordResetTokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(resetToken)));
            sales.PasswordResetExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
            await db.SaveChangesAsync();
        }

        var resetResponse = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequestDto(resetToken, newPassword));
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var oldPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", CarInsuranceApiFactory.DemoPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordResponse.StatusCode);

        var newPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto("sales", newPassword));
        Assert.Equal(HttpStatusCode.OK, newPasswordResponse.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequestDto("invalid-reset-token", "NewDemo456!"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResetPassword_WithShortPassword_ReturnsBadRequest()
    {
        using var factory = new CarInsuranceApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/reset-password",
            new ResetPasswordRequestDto("any-token", "short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

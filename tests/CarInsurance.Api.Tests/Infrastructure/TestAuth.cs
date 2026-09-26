using System.Net.Http.Headers;
using System.Net.Http.Json;
using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Tests.Infrastructure;

public static class TestAuth
{
    public static readonly IReadOnlyDictionary<string, string> DemoRoles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sales"] = "業務員",
            ["underwriter"] = "核保員",
            ["claims"] = "理賠員",
            ["manager"] = "主管",
            ["admin"] = "系統管理員"
        };

    public static async Task<AuthTokenResponseDto> LoginAsAsync(
        HttpClient client,
        string account,
        CancellationToken cancellationToken = default)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto(account, CarInsuranceApiFactory.DemoPassword),
            cancellationToken);
        loginResponse.EnsureSuccessStatusCode();

        var challenge = await loginResponse.Content.ReadFromJsonAsync<MfaChallengeDto>(cancellationToken)
            ?? throw new InvalidOperationException("Login 未回傳 MFA challenge。");

        var verifyResponse = await client.PostAsJsonAsync(
            "/api/auth/mfa/verify",
            new MfaVerifyRequestDto(challenge.MfaToken, CarInsuranceApiFactory.MfaCode),
            cancellationToken);
        verifyResponse.EnsureSuccessStatusCode();

        return await verifyResponse.Content.ReadFromJsonAsync<AuthTokenResponseDto>(cancellationToken)
            ?? throw new InvalidOperationException("MFA verify 未回傳 access token。");
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(
        CarInsuranceApiFactory factory,
        string account,
        CancellationToken cancellationToken = default)
    {
        var client = factory.CreateClient();
        var token = await LoginAsAsync(client, account, cancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}

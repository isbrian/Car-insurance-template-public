using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SecurityClaim = System.Security.Claims.Claim;
using System.Security.Cryptography;
using System.Text;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace CarInsurance.Api.Services;

public sealed class AuthService(
    IAuthRepository repository,
    IConfiguration configuration,
    IWebHostEnvironment environment) : IAuthService
{
    private const string AccessTokenType = "access";
    private const string MfaTokenType = "mfa";
    private const string TokenTypeClaim = "token_type";
    private const string DisplayNameClaim = "display_name";

    private readonly PasswordHasher<UserAccount> _passwordHasher = new();

    public async Task<MfaChallengeDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var account = RequireText(request.Account, "account");
        var password = RequireText(request.Password, "password");
        var user = await repository.GetByAccountAsync(account, cancellationToken);

        if (user is null || !user.IsActive ||
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new AuthUnauthorizedException("帳號或密碼錯誤。");
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(GetPositiveInt("Jwt:MfaTokenMinutes", 5));
        var mfaToken = CreateToken(user, MfaTokenType, expiresAtUtc);

        return new MfaChallengeDto(
            true,
            mfaToken,
            MaskPhone(user.Phone),
            expiresAtUtc,
            false);
    }

    public async Task<AuthTokenResponseDto> VerifyMfaAsync(MfaVerifyRequestDto request, CancellationToken cancellationToken)
    {
        var mfaToken = RequireText(request.MfaToken, "mfaToken");
        var code = RequireText(request.Code, "code");
        var expectedCode = GetRequiredSetting("Auth:MfaCode");

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code),
                Encoding.UTF8.GetBytes(expectedCode)))
        {
            throw new AuthUnauthorizedException("MFA 驗證失敗。");
        }

        ClaimsPrincipal principal;
        try
        {
            principal = ValidateToken(mfaToken);
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            throw new AuthUnauthorizedException("MFA token 無效或已過期。");
        }

        if (principal.FindFirstValue(TokenTypeClaim) != MfaTokenType ||
            !int.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
        {
            throw new AuthUnauthorizedException("MFA token 無效或已過期。");
        }

        var user = await repository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthUnauthorizedException("MFA token 無效或已過期。");
        }

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(GetPositiveInt("Jwt:AccessTokenMinutes", 60));
        return new AuthTokenResponseDto(
            CreateToken(user, AccessTokenType, expiresAtUtc),
            "Bearer",
            expiresAtUtc,
            ToUserDto(user));
    }

    public async Task<ForgotPasswordResponseDto> ForgotPasswordAsync(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        var account = RequireText(request.Account, "account");
        var user = await repository.GetByAccountForUpdateAsync(account, cancellationToken);
        var demoToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(GetPositiveInt("Auth:PasswordResetMinutes", 30));

        if (user is not null && user.IsActive)
        {
            user.PasswordResetTokenHash = HashToken(demoToken);
            user.PasswordResetExpiresAtUtc = expiresAtUtc;
            user.UpdatedAtUtc = DateTime.UtcNow;
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new ForgotPasswordResponseDto(
            "若帳號存在，系統已建立密碼重設請求。",
            environment.IsDevelopment() ? demoToken : null,
            environment.IsDevelopment() ? expiresAtUtc : null);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken)
    {
        var token = RequireText(request.Token, "token");
        var password = RequireText(request.NewPassword, "newPassword");
        if (password.Length < 8)
        {
            throw new AuthValidationException("newPassword 至少需要 8 個字元。");
        }

        var tokenHash = HashToken(token);
        var user = await repository.GetByResetTokenHashForUpdateAsync(tokenHash, cancellationToken);
        if (user is null || user.PasswordResetExpiresAtUtc is null || user.PasswordResetExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new AuthValidationException("密碼重設 token 無效或已過期。");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpiresAtUtc = null;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedUserDto?> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(userId, cancellationToken);
        return user is null || !user.IsActive ? null : ToUserDto(user);
    }

    private string CreateToken(UserAccount user, string tokenType, DateTime expiresAtUtc)
    {
        var claims = new List<SecurityClaim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new("name", user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("role", user.Role),
            new(DisplayNameClaim, user.DisplayName),
            new(TokenTypeClaim, tokenType)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey())),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: GetRequiredSetting("Jwt:Issuer"),
            audience: GetRequiredSetting("Jwt:Audience"),
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, CreateTokenValidationParameters(), out _);
    }

    private TokenValidationParameters CreateTokenValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = GetRequiredSetting("Jwt:Issuer"),
        ValidateAudience = true,
        ValidAudience = GetRequiredSetting("Jwt:Audience"),
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetSigningKey())),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "name",
        RoleClaimType = "role"
    };

    private string GetSigningKey()
    {
        var key = GetRequiredSetting("Jwt:SigningKey");
        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey 至少需要 32 bytes。");
        }
        return key;
    }

    private string GetRequiredSetting(string key) =>
        configuration[key] ?? throw new InvalidOperationException($"缺少必要設定 {key}。");

    private int GetPositiveInt(string key, int fallback)
    {
        var value = configuration.GetValue<int?>(key);
        return value is > 0 ? value.Value : fallback;
    }

    private static string RequireText(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AuthValidationException($"{fieldName} 不可為空白。");
        }
        return value.Trim();
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string MaskPhone(string phone)
    {
        var normalized = phone.Trim();
        return normalized.Length >= 5
            ? $"{normalized[..2]}**-***-{normalized[^3..]}"
            : "***";
    }

    private static AuthenticatedUserDto ToUserDto(UserAccount user) =>
        new(user.Id, user.Username, user.DisplayName, user.Email, user.Role);
}

using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record LoginRequestDto(
    [Required] string Account,
    [Required] string Password,
    bool RememberDevice = false);

public sealed record MfaChallengeDto(
    bool RequiresMfa,
    string MfaToken,
    string MaskedPhone,
    DateTime ExpiresAtUtc,
    bool RememberDeviceSupported);

public sealed record MfaVerifyRequestDto(
    [Required] string MfaToken,
    [Required] string Code);

public sealed record AuthenticatedUserDto(
    int Id,
    string Username,
    string DisplayName,
    string Email,
    string Role);

public sealed record AuthTokenResponseDto(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    AuthenticatedUserDto User);

public sealed record ForgotPasswordRequestDto(
    [Required] string Account);

public sealed record ForgotPasswordResponseDto(
    string Message,
    string? DemoResetToken,
    DateTime? ExpiresAtUtc);

public sealed record ResetPasswordRequestDto(
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

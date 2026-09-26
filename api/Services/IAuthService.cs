using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IAuthService
{
    Task<MfaChallengeDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken);
    Task<AuthTokenResponseDto> VerifyMfaAsync(MfaVerifyRequestDto request, CancellationToken cancellationToken);
    Task<ForgotPasswordResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request, CancellationToken cancellationToken);
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken);
    Task<AuthenticatedUserDto?> GetUserAsync(int userId, CancellationToken cancellationToken);
}

public sealed class AuthUnauthorizedException(string message) : Exception(message);
public sealed class AuthValidationException(string message) : Exception(message);

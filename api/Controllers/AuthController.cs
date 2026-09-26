using System.IdentityModel.Tokens.Jwt;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService service) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<MfaChallengeDto>> Login(
        LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.LoginAsync(request, cancellationToken));
        }
        catch (AuthUnauthorizedException ex)
        {
            return UnauthorizedProblem(ex.Message);
        }
        catch (AuthValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [HttpPost("mfa/verify")]
    public async Task<ActionResult<AuthTokenResponseDto>> VerifyMfa(
        MfaVerifyRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.VerifyMfaAsync(request, cancellationToken));
        }
        catch (AuthUnauthorizedException ex)
        {
            return UnauthorizedProblem(ex.Message);
        }
        catch (AuthValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponseDto>> ForgotPassword(
        ForgotPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Accepted(await service.ForgotPasswordAsync(request, cancellationToken));
        }
        catch (AuthValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await service.ResetPasswordAsync(request, cancellationToken);
            return Ok(new { message = "密碼已重設。" });
        }
        catch (AuthValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUserDto>> Me(CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(subject, out var userId))
        {
            return UnauthorizedProblem("Access token 無效。");
        }

        var user = await service.GetUserAsync(userId, cancellationToken);
        return user is null ? UnauthorizedProblem("Access token 無效。") : Ok(user);
    }

    private ActionResult Invalid(string detail) =>
        Problem(statusCode: 400, title: "Auth 資料無效", detail: detail);

    private ActionResult UnauthorizedProblem(string detail) =>
        Problem(statusCode: 401, title: "身分驗證失敗", detail: detail);
}

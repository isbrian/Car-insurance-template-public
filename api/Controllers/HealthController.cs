using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

/// <summary>
/// 健康檢查端點，供 Docker healthcheck 與佈署後的存活確認使用。
/// </summary>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        // ponytail: stdlib: Assembly 取版本 | upgrade if: 需要 build number 或 commit hash，改由 CI 注入環境變數
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";

        return Ok(new
        {
            status = "ok",
            version,
            timestamp = DateTime.UtcNow
        });
    }
}

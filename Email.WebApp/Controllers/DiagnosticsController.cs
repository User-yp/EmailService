using Email.Domain.IApplication;
using Email.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Email.WebApp.Controllers;

/// <summary>
/// 依赖组件连通性诊断接口（与 WebApi 共用应用层 ISystemDiagnosticsService）。
/// </summary>
[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly ISystemDiagnosticsService _diagnosticsService;
    private readonly IWebHostEnvironment _environment;

    public DiagnosticsController(ISystemDiagnosticsService diagnosticsService, IWebHostEnvironment environment)
    {
        _diagnosticsService = diagnosticsService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<object>> Get(CancellationToken cancellationToken)
    {
        var diagnostics = await _diagnosticsService.CheckAsync(cancellationToken);

        return Ok(new
        {
            environment = _environment.EnvironmentName,
            timestamp = diagnostics.Timestamp,
            allHealthy = diagnostics.AllHealthy,
            items = diagnostics.Items.Select(i => new
            {
                target = i.Target,
                success = i.Success,
                message = i.Message,
                elapsedMilliseconds = i.ElapsedMilliseconds
            })
        });
    }
}

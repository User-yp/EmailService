using Email.Domain.IApplication;
using Email.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace Email.WebApp.Controllers;

/// <summary>
/// 开发/演示用测试数据生成接口（直接写库、不经过 SMTP）。
/// </summary>
[ApiController]
[Route("api/seed")]
public class SeedController : ControllerBase
{
    private readonly IDataSeeder _dataSeeder;
    private readonly ILogger<SeedController> _logger;

    public SeedController(IDataSeeder dataSeeder, ILogger<SeedController> logger)
    {
        _dataSeeder = dataSeeder;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<SeedResult>> Post([FromQuery] int count = 24, [FromQuery] bool append = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dataSeeder.SeedAsync(count, append, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成测试数据失败");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"生成测试数据失败：{ex.Message}" });
        }
    }
}

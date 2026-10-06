using Email.Domain.IRepository;
using Email.Domain.Models;
using Email.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace Email.WebApp.Controllers;

/// <summary>
/// 概览数据接口。表现层只做编排，统计口径由基础设施层的读模型查询负责。
/// </summary>
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IEmailRepository _emailRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IEmailRepository emailRepository, ILogger<DashboardController> logger)
    {
        _emailRepository = emailRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken)
    {
        try
        {
            var statistics = await _emailRepository.GetStatisticsAsync(cancellationToken);
            var recent = await _emailRepository.SearchAsync(
                new EmailQueryFilter { Page = 1, PageSize = 8 }, cancellationToken);

            return Ok(new DashboardDto
            {
                Statistics = statistics,
                RecentEmails = recent.Items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载概览数据失败");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"读取数据库失败：{ex.Message}" });
        }
    }
}

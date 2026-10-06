using Email.Domain.Entity;
using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Infrastructure;
using Email.Infrastructure.Factory;
using Email.Extension.Option;
using Email.WebApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Diagnostics;

namespace Email.WebApi.Controllers;

/// <summary>
/// 联调自测控制器：覆盖邮件发送、状态查询、重试、软删除、附件与 FTP 操作，以及依赖组件连通性诊断。
/// 仅供开发联调使用，生产环境请替换为带认证与参数校验的业务接口。
/// </summary>
[ApiController]
[Route("[controller]/[action]")]
public class TestController : ControllerBase
{
    private readonly IDomainService _domainService;
    private readonly IEmailRepository _emailRepository;
    private readonly IEmailHandler _emailHandler;
    private readonly IFtpService _ftpService;
    private readonly SmtpClientFactory _smtpClientFactory;
    private readonly IDbContextFactory<EmailDbContext> _dbContextFactory;
    private readonly RedisOption _redisOption;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TestController> _logger;

    public TestController(
        IDomainService domainService,
        IEmailRepository emailRepository,
        IEmailHandler emailHandler,
        IFtpService ftpService,
        SmtpClientFactory smtpClientFactory,
        IDbContextFactory<EmailDbContext> dbContextFactory,
        RedisOption redisOption,
        IWebHostEnvironment environment,
        ILogger<TestController> logger)
    {
        _domainService = domainService;
        _emailRepository = emailRepository;
        _emailHandler = emailHandler;
        _ftpService = ftpService;
        _smtpClientFactory = smtpClientFactory;
        _dbContextFactory = dbContextFactory;
        _redisOption = redisOption;
        _environment = environment;
        _logger = logger;
    }

    // ---------------------------------------------------------------- 诊断

    /// <summary>
    /// 一次性检查 SMTP / FTP / SQL Server / Redis 四项依赖的连通性。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> DiagnosticsAsync(CancellationToken cancellationToken)
    {
        var smtp = await ProbeAsync("SMTP", async () =>
        {
            var ok = await _smtpClientFactory.TestConnectionAsync();
            return (ok, ok ? "SMTP 连接与认证正常" : "SMTP 连接或认证失败，详见服务日志");
        });

        var ftp = await ProbeAsync("FTP", async () =>
        {
            var ok = await _ftpService.TestConnectionAsync();
            return (ok, ok ? "FTP 连接正常" : "FTP 连接失败，详见服务日志");
        });

        var database = await ProbeAsync("Database", async () =>
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            var ok = await context.Database.CanConnectAsync(cancellationToken);
            return (ok, ok ? "数据库可连接" : "数据库不可连接");
        });

        var redis = await ProbeAsync("Redis", async () =>
        {
            using var connection = await ConnectionMultiplexer.ConnectAsync(_redisOption.ConnectionString);
            var latency = await connection.GetDatabase(_redisOption.DbNumber).PingAsync();
            return (true, $"Redis PING 往返 {latency.TotalMilliseconds:F0} ms");
        });

        return Ok(new DiagnosticsResponse
        {
            Environment = _environment.EnvironmentName,
            Smtp = smtp,
            Ftp = ftp,
            Database = database,
            Redis = redis
        });
    }

    /// <summary>
    /// 单独测试 SMTP 连接与认证（不发送邮件）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> SmtpTestAsync()
    {
        var success = await _smtpClientFactory.TestConnectionAsync();
        return success
            ? Ok(new { success, message = "SMTP 连接与认证正常" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { success, message = "SMTP 连接失败，详见服务日志" });
    }

    // ---------------------------------------------------------------- 发送

    /// <summary>
    /// 发送邮件（JSON 请求体，无附件）。走完整链路：入库 → SMTP 发送 → 附件归档。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendEmailAsync([FromBody] SendEmailRequest request, CancellationToken cancellationToken)
    {
        return await SendAsync(request, [], cancellationToken);
    }

    /// <summary>
    /// 发送带附件的邮件（multipart/form-data，可传 0~N 个附件）。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendEmailWithAttachmentsAsync([FromForm] SendEmailFormRequest request, CancellationToken cancellationToken)
    {
        var attachments = new List<Attachment>();
        foreach (var file in request.Files ?? [])
        {
            if (file.Length == 0)
                continue;

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);

            attachments.Add(Attachment.Create(
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                stream.ToArray()));
        }

        return await SendAsync(request, attachments, cancellationToken);
    }

    /// <summary>
    /// 不经数据库直接调用 SMTP 发送，用于把 SMTP 故障与数据库故障区分开。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendRawAsync([FromBody] SendEmailRequest request)
    {
        var message = EmailMessage.Create(request.To, request.Cc, request.Bcc, request.From,
            request.Subject, request.Body, null, request.IsHtml);

        try
        {
            await _emailHandler.SendEmailAsync(message);
            return Ok(new { success = true, message = "SMTP 发送成功（未写入数据库）" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP 直发失败");
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, error = ex.Message });
        }
    }

    // ---------------------------------------------------------------- 查询

    /// <summary>
    /// 按邮件 Id 查询发送状态、附件与失败原因。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetEmailStatusAsync(Guid emailId)
    {
        var email = await _domainService.GetEmailStatusAsync(emailId);
        return email == null
            ? NotFound(new { emailId, message = "邮件不存在或已软删除" })
            : Ok(email.ToResponse());
    }

    /// <summary>
    /// 按创建时间倒序列出最近的邮件。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllEmailsAsync(int take = 20, CancellationToken cancellationToken = default)
    {
        var emails = await _emailRepository.GetRecentAsync(take, cancellationToken);
        return Ok(emails.Select(e => e.ToResponse()));
    }

    /// <summary>
    /// 查询附件元数据；download=true 时直接下载文件（内容已归档到 FTP 时自动从 FTP 取回）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAttachmentAsync(Guid attachmentId, bool download = false, CancellationToken cancellationToken = default)
    {
        var attachment = await _domainService.GetEmailAttachmentAsync(attachmentId);
        if (attachment == null)
            return NotFound(new { attachmentId, message = "附件不存在或已软删除" });

        var inDatabase = attachment.Content.Length > 0;
        var fromFtp = !inDatabase && attachment.IsStoredInFtp && !string.IsNullOrEmpty(attachment.FilePath);

        if (!download)
        {
            return Ok(new
            {
                attachment.Id,
                attachment.FileName,
                attachment.ContentType,
                attachment.FileSize,
                attachment.IsStoredInFtp,
                attachment.FilePath,
                source = inDatabase ? "database" : fromFtp ? "ftp" : "none"
            });
        }

        if (!inDatabase && !fromFtp)
            return NotFound(new { attachmentId, message = "附件内容为空且未归档到 FTP" });

        var stream = inDatabase
            ? new MemoryStream(attachment.Content)
            : await _ftpService.DownloadFileAsync(attachment.FilePath!);

        return File(stream, attachment.ContentType, attachment.FileName);
    }

    // ---------------------------------------------------------------- 重试 / 软删除

    /// <summary>
    /// 手动重试一封发送失败的邮件（受最大重试次数与冷却时间约束）。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RetryAsync(Guid emailId)
    {
        var success = await _domainService.RetryEmailAsync(emailId);
        var state = await _domainService.GetEmailStatusAsync(emailId);

        return Ok(new
        {
            emailId,
            success,
            hint = success ? "重试成功" : "未重试成功：可能未达条件（次数/冷却）或再次发送失败",
            state = state?.ToResponse()
        });
    }

    /// <summary>
    /// 软删除邮件（连同附件与发送记录，数据物理保留）。
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteAsync(Guid emailId)
    {
        var deleted = await _domainService.DeleteEmailAsync(emailId);

        return deleted
            ? Ok(new { emailId, deleted, note = "软删除完成：数据物理保留，查询接口不再返回" })
            : NotFound(new { emailId, deleted, note = "邮件不存在或已删除" });
    }

    // ---------------------------------------------------------------- FTP

    /// <summary>
    /// 上传文件到 FTP，返回远程路径（供 FtpExistsAsync / FtpDownloadAsync 验证）。
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> FtpUploadAsync(IFormFile file, Guid? emailId = null, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "请上传非空文件" });

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        stream.Position = 0; // CopyTo 后位置在末尾，必须回到开头才能正确上传

        try
        {
            var remotePath = await _ftpService.UploadFileAsync(stream, file.FileName, emailId ?? Guid.NewGuid());
            return Ok(new { file.FileName, size = file.Length, remotePath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "上传文件到 FTP 失败：{FileName}", file.FileName);
            return StatusCode(StatusCodes.Status502BadGateway, new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// 判断 FTP 上的文件是否存在。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FtpExistsAsync(string path)
    {
        var exists = await _ftpService.FileExistsAsync(path);
        return Ok(new { path, exists });
    }

    /// <summary>
    /// 列出 FTP 目录下的文件名。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FtpListAsync(string path)
    {
        try
        {
            var files = await _ftpService.ListFilesAsync(path);
            return Ok(new { path, count = files.Count, files });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "列出 FTP 目录失败：{Path}", path);
            return NotFound(new { path, error = ex.Message });
        }
    }

    /// <summary>
    /// 从 FTP 下载文件。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> FtpDownloadAsync(string path)
    {
        try
        {
            var stream = await _ftpService.DownloadFileAsync(path);
            return File(stream, "application/octet-stream", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载 FTP 文件失败：{Path}", path);
            return NotFound(new { path, error = ex.Message });
        }
    }

    /// <summary>
    /// 删除 FTP 上的文件。
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> FtpDeleteAsync(string path)
    {
        var deleted = await _ftpService.DeleteFileAsync(path);
        return deleted
            ? Ok(new { path, deleted })
            : NotFound(new { path, deleted, note = "文件不存在或删除失败" });
    }

    // ---------------------------------------------------------------- 内部

    private async Task<IActionResult> SendAsync(SendEmailRequest request, List<Attachment> attachments, CancellationToken cancellationToken)
    {
        try
        {
            var emailId = await _domainService.SendEmailAsync(request.To, request.Cc, request.Bcc, request.From,
                request.Subject, request.Body, attachments, request.IsHtml);

            // 回读一次：发送成功后附件可能已被归档到 FTP（Content 清空、FilePath 填充）
            var state = await _domainService.GetEmailStatusAsync(emailId);
            return Ok(state?.ToResponse());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送邮件失败，收件人：{To}", string.Join(", ", request.To));

            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                success = false,
                error = ex.Message,
                hint = "邮件已以 Failed 状态入库，可用 GetAllEmailsAsync 查询后用 RetryAsync 重试。"
            });
        }
    }

    /// <summary>
    /// 执行一次探测：返回 (是否通过, 说明)；抛异常时按失败处理。
    /// </summary>
    private async Task<DiagnosticItem> ProbeAsync(string target, Func<Task<(bool Success, string Message)>> probe)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (success, message) = await probe();
            return new DiagnosticItem
            {
                Success = success,
                Message = message,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Target} 连通性检查失败", target);
            return new DiagnosticItem
            {
                Success = false,
                Message = $"{ex.GetType().Name}: {ex.Message}",
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }
}

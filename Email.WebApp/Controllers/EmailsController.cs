using Email.Domain;
using Email.Domain.Entity;
using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Email.Domain.Models;
using Email.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace Email.WebApp.Controllers;

/// <summary>
/// 邮件相关的 JSON 接口：列表、详情、发送、重试、软删除。
/// </summary>
[ApiController]
[Route("api/emails")]
public class EmailsController : ControllerBase
{
    private const int MaxAttachmentBytes = 30 * 1024 * 1024;

    private readonly IEmailRepository _emailRepository;
    private readonly IDomainService _domainService;
    private readonly ILogger<EmailsController> _logger;

    public EmailsController(IEmailRepository emailRepository, IDomainService domainService,
        ILogger<EmailsController> logger)
    {
        _emailRepository = emailRepository;
        _domainService = domainService;
        _logger = logger;
    }

    /// <summary>分页查询邮件列表。</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmailSummary>>> Get(
        [FromQuery] EmailStatus? status,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = EmailQueryFilter.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var filter = new EmailQueryFilter
        {
            Status = status,
            Keyword = keyword,
            Page = page,
            PageSize = pageSize
        };

        try
        {
            var result = await _emailRepository.SearchAsync(filter, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询邮件列表失败");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"查询失败：{ex.Message}" });
        }
    }

    /// <summary>按 Id 查询邮件详情（含发送记录与附件）。</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmailDetailDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var message = await _emailRepository.GetByIdWithAttachmentsAsync(id);
            if (message == null)
                return NotFound(new { error = "邮件不存在，或已被软删除。" });

            return Ok(message.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "查询邮件详情失败：{EmailId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = $"查询邮件详情失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 发送测试邮件（multipart/form-data）：走完整链路（入库 → SMTP 发送 → 附件归档）。
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxAttachmentBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxAttachmentBytes)]
    public async Task<ActionResult<EmailDetailDto>> Send([FromForm] SendEmailForm form, CancellationToken cancellationToken)
    {
        var recipients = SendEmailForm.SplitAddresses(form.To);
        if (recipients == null)
            return BadRequest(new { error = "请至少填写一个收件人。" });

        if (string.IsNullOrWhiteSpace(form.Subject))
            return BadRequest(new { error = "请填写主题。" });

        if (string.IsNullOrWhiteSpace(form.Body))
            return BadRequest(new { error = "请填写正文。" });

        try
        {
            var attachments = await ReadAttachmentsAsync(form.Files, cancellationToken);

            var emailId = await _domainService.SendEmailAsync(
                recipients,
                SendEmailForm.SplitAddresses(form.Cc),
                SendEmailForm.SplitAddresses(form.Bcc),
                string.IsNullOrWhiteSpace(form.From) ? null : form.From.Trim(),
                form.Subject,
                form.Body,
                attachments,
                form.IsHtml);

            // 回读一次：发送成功后附件可能已被归档到 FTP（内容清空、写入远程路径）
            var message = await _emailRepository.GetByIdWithAttachmentsAsync(emailId);
            return message == null ? Ok(new { emailId }) : Ok(message.ToDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "发送测试邮件失败");
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = $"发送失败：{ex.Message}",
                hint = "该邮件已以 Failed 状态入库，可在列表中重试。"
            });
        }
    }

    /// <summary>手动重试（是否满足重试条件由领域层裁决）。</summary>
    [HttpPost("{id:guid}/retry")]
    public async Task<ActionResult<object>> Retry(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var success = await _domainService.RetryEmailAsync(id);
            return Ok(new
            {
                emailId = id,
                success,
                message = success ? "重试成功，邮件已重新发送。" : "未重试成功：可能未满足重试条件（次数上限 / 冷却期），或本次发送依然失败。"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重试邮件失败：{EmailId}", id);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = $"重试失败：{ex.Message}" });
        }
    }

    /// <summary>软删除（数据物理保留，仅置位删除标记）。</summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<object>> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _domainService.DeleteEmailAsync(id);
            return deleted
                ? Ok(new { emailId = id, deleted, message = "邮件已软删除（数据物理保留）。" })
                : NotFound(new { error = "删除失败：邮件不存在或已删除。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "软删除邮件失败：{EmailId}", id);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = $"删除失败：{ex.Message}" });
        }
    }

    private static async Task<List<Attachment>> ReadAttachmentsAsync(List<IFormFile>? files, CancellationToken cancellationToken)
    {
        var attachments = new List<Attachment>();
        if (files == null)
            return attachments;

        foreach (var file in files.Where(f => f.Length > 0))
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream, cancellationToken);

            attachments.Add(Attachment.Create(
                file.FileName,
                string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                stream.ToArray()));
        }

        return attachments;
    }
}

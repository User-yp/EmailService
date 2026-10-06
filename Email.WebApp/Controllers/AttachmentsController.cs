using Email.Domain.IApplication;
using Email.Domain.IRepository;
using Microsoft.AspNetCore.Mvc;

namespace Email.WebApp.Controllers;

/// <summary>
/// 附件下载：数据库里还有内容就直接下发，已归档到 FTP 的自动回源。
/// </summary>
[ApiController]
[Route("api/attachments")]
public class AttachmentsController : ControllerBase
{
    private readonly IDomainService _domainService;
    private readonly IFtpService _ftpService;
    private readonly ILogger<AttachmentsController> _logger;

    public AttachmentsController(IDomainService domainService, IFtpService ftpService,
        ILogger<AttachmentsController> logger)
    {
        _domainService = domainService;
        _ftpService = ftpService;
        _logger = logger;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var attachment = await _domainService.GetEmailAttachmentAsync(id);
        if (attachment == null)
            return NotFound(new { error = "附件不存在或已软删除。" });

        if (attachment.Content.Length > 0)
            return File(new MemoryStream(attachment.Content), attachment.ContentType, attachment.FileName);

        if (attachment.IsStoredInFtp && !string.IsNullOrEmpty(attachment.FilePath))
        {
            try
            {
                var stream = await _ftpService.DownloadFileAsync(attachment.FilePath);
                return File(stream, attachment.ContentType, attachment.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从 FTP 取回附件失败：{AttachmentId}", id);
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { error = $"从 FTP 取回附件失败：{ex.Message}" });
            }
        }

        return NotFound(new { error = "附件内容为空且未归档到 FTP。" });
    }
}

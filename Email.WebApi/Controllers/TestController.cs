using Email.Domain.Entity;
using Email.Domain.IRepository;
using Email.Infrastructure.Application;
using Email.Infrastructure.Repository;
using Microsoft.AspNetCore.Mvc;

namespace Email.WebApi.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class TestController : ControllerBase
{
    private readonly IEmailHandler mailKit;
    private readonly DomainService emailApp;
    private readonly IFtpService ftpService;

    public TestController(IEmailHandler mailKit, DomainService emailApp, IFtpService ftpService)
    {
        this.mailKit = mailKit;
        this.emailApp = emailApp;
        this.ftpService = ftpService;
    }

    [HttpPut]
    public async Task<IActionResult> FTPTestAsync(IFormFile file)
    {
        //文件操作
        using var memoryStream = new MemoryStream();
        file.CopyTo(memoryStream);

        var res = await ftpService.UploadFileAsync(memoryStream, file.FileName, Guid.NewGuid());
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> AttachmentsAsync(List<IFormFile> files)
    {
        //文件操作
        using var memoryStream = new MemoryStream();
        var file = files.First();
        file.CopyTo(memoryStream);

        var att = Attachment.Create(file.FileName, file.ContentType, memoryStream.ToArray());

        await emailApp.SendEmailAsync(["your email"],
            null,
            null,
            "your email",
            "Http邮件测试",
            $"这是测试内容",
            [att],
            false);
        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> AttachmentAsync(IFormFile file)
    {
        //文件操作
        using var memoryStream = new MemoryStream();
        file.CopyTo(memoryStream);

        var att = Attachment.Create(
            file.FileName,
            file.ContentType,
            memoryStream.ToArray());

        await emailApp.SendEmailAsync(["your email"],
            null,
            null,
            "your email",
            "Http邮件测试",
            $"这是测试内容",
            [att],
            false);
        return Ok();
    }

    [HttpPut]
    public async Task<IActionResult> SendEmailAsync()
    {
        await mailKit.SendEmailAsync(EmailMessage.Create(
            ["your email",
                "your email"],
            null,
            null,
            "your email",
            "Http邮件测试",
            $"这是测试内容",
            new List<Attachment>(),
            false));
        return Ok();
    }
}


using Email.Infrastructure.Application;
using Grpc.Core;
namespace Email.RPCServe;


public class GrpcEmailService : EmailService.EmailServiceBase
{
    private readonly DomainService _emailApp;

    public GrpcEmailService(DomainService emailApp)
    {
        this._emailApp = emailApp;
    }
    public override async Task<EmailResponse> SendEmail(EmailRequest request, ServerCallContext context)
    {
        try
        {
            // 转换gRPC消息为领域模型
            var attachments = request.Attachments.Select(att =>
                Domain.Entity.Attachment.Create(
                    att.FileName,
                    att.ContentType,
                    att.Content.ToByteArray()
                )).ToList();

            // 调用现有的应用服务
            await _emailApp.SendEmailAsync(
                request.To.ToList(),
                request.Cc?.ToList(),
                request.Bcc?.ToList(),
                request.From,
                request.Subject,
                request.Body,
                attachments,
                request.IsHtml
            );

            return new EmailResponse
            {
                Success = true,
                MessageId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            return new EmailResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public override async Task<EmailResponse> SendEmailWithAttachment(
        IAsyncStreamReader<AttachmentChunk> requestStream,
        ServerCallContext context)
    {
        try
        {
            EmailRequest? metadata = null;
            Dictionary<string, MemoryStream> fileStreams = new();

            // 处理流式请求
            while (await requestStream.MoveNext())
            {
                var chunk = requestStream.Current;

                if (chunk.DataCase == AttachmentChunk.DataOneofCase.Metadata)
                {
                    metadata = chunk.Metadata;
                }
                else if (chunk.DataCase == AttachmentChunk.DataOneofCase.Chunk)
                {
                    var fileChunk = chunk.Chunk;

                    if (!fileStreams.ContainsKey(fileChunk.FileName))
                    {
                        fileStreams[fileChunk.FileName] = new MemoryStream();
                    }

                    await fileStreams[fileChunk.FileName].WriteAsync(
                        fileChunk.ChunkData.Memory,
                        context.CancellationToken
                    );
                }
            }

            if (metadata == null)
            {
                throw new InvalidOperationException("No metadata provided");
            }

            // 转换附件
            var attachments = new List<Domain.Entity.Attachment>();
            foreach (var kvp in fileStreams)
            {
                var bytes = kvp.Value.ToArray();
                attachments.Add(Domain.Entity.Attachment.Create(
                    kvp.Key,
                    metadata.Attachments.FirstOrDefault(a => a.FileName == kvp.Key)?.ContentType
                        ?? "application/octet-stream",
                    bytes
                ));
            }

            // 调用现有的应用服务
            await _emailApp.SendEmailAsync(
                metadata.To.ToList(),
                metadata.Cc?.ToList(),
                metadata.Bcc?.ToList(),
                metadata.From,
                metadata.Subject,
                metadata.Body,
                attachments,
                metadata.IsHtml
            );

            return new EmailResponse
            {
                Success = true,
                MessageId = Guid.NewGuid().ToString()
            };
        }
        catch (Exception ex)
        {
            return new EmailResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

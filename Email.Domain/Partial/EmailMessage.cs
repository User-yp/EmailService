namespace Email.Domain.Entity;

public partial class EmailMessage
{
    public static EmailMessage Create(List<string> to, List<string>? cc, List<string>? bcc, string? from, string subject, string body, List<Attachment>? attachments, bool isHtml)
    {
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentNullException(subject);
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentNullException(body);
        if (to == null || to.Count == 0)
            throw new ArgumentException("At least one To address is required.", nameof(to));

        var emailMessage = new EmailMessage(to, cc, bcc, from, subject, body, attachments, isHtml);

        return emailMessage;
    }
    public void AddAttachment(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);


        attachment.AssociateWithEmail(Id);
        Attachments.Add(attachment);
    }

    public void AddAttachments(List<Attachment>? attachments)
    {
        if (attachments == null || attachments.Count == 0)
            return;

        foreach (var attachment in attachments)
        {
            AddAttachment(attachment);
        }
    }

    // 通过EmailMessage操作EmailRecord的方法
    public void MarkAsSent()
    {
        Record.MarkAsSent();
        foreach (var attachment in Attachments)
        {
            if (attachment.IsStoredInFtp)
                attachment.ClearContent();
        }
    }
    public void MarkAsFailed(string errorMessage = null, string errorDetails = null)
        => Record.MarkAsFailed(errorMessage, errorDetails);
    public void MarkForRetry(List<string> failedAdress, string errorMessage = null, string errorDetails = null)
        => Record.MarkForRetry(failedAdress, errorMessage, errorDetails);

    public bool CanRetry(int maxRetryCount = 3, TimeSpan? cooldownPeriod = null)
        => Record.CanRetry(maxRetryCount, cooldownPeriod);
}

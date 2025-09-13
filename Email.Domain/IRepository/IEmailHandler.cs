using Email.Domain.Entity;

namespace Email.Domain.IRepository;

public interface IEmailHandler
{
    Task SendEmailAsync(EmailMessage emailMessage);
}
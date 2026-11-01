using Application.Common.Models;

namespace Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(
            string to,
            string subject,
            string htmlContent,
            IReadOnlyList<EmailAttachmentDto>? attachments = null);

        Task SendEmailAsync(
            IEnumerable<string> recipients,
            string subject,
            string htmlContent,
            IReadOnlyList<EmailAttachmentDto>? attachments = null);
    }
}

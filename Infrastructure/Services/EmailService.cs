using Application.Common.Interfaces;
using Application.Common.Models;
using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailClient _emailClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _senderAddress;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var connectionString = _configuration["AzureCommunicationServices:ConnectionString"];
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("Azure Communication Services connection string is not configured");

            _emailClient = new EmailClient(connectionString);
            _senderAddress = _configuration["AzureCommunicationServices:SenderAddress"]
                ?? throw new InvalidOperationException("Sender email address is not configured");
        }

        public async Task SendEmailAsync(
            string to,
            string subject,
            string htmlContent,
            IReadOnlyList<EmailAttachmentDto>? attachments = null)
        {
            await SendEmailAsync([to], subject, htmlContent, attachments);
        }

        public async Task SendEmailAsync(
            IEnumerable<string> recipients,
            string subject,
            string htmlContent,
            IReadOnlyList<EmailAttachmentDto>? attachments = null)
        {
            try
            {
                var emailContent = new EmailContent(subject)
                {
                    Html = htmlContent
                };

                var emailRecipients = new EmailRecipients(
                    recipients.Select(r => new EmailAddress(r)).ToList());

                var emailMessage = new EmailMessage(_senderAddress, emailRecipients, emailContent);

                if (attachments != null)
                {
                    foreach (var attachment in attachments)
                    {
                        emailMessage.Attachments.Add(new EmailAttachment(
                            name: attachment.FileName,
                            contentType: attachment.ContentType,
                            content: BinaryData.FromBytes(attachment.Content)));
                    }
                }

                var operation = await _emailClient.SendAsync(WaitUntil.Started, emailMessage);

                _logger.LogInformation(
                    "Email queued. MessageId: {MessageId} | Recipients: {Recipients} | Attachments: {AttachmentCount}",
                    operation.Id,
                    string.Join(", ", recipients),
                    attachments?.Count ?? 0);
            }
            catch (RequestFailedException ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", recipients));
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
        }
    }
}

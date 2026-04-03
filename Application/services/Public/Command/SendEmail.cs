using System.Net;
using Application.Common.Interfaces;
using Application.Common.Settings;
using Application.Services.Public.Request;
using Domain.Entity.Coworking;
using MediatR;
using Microsoft.Extensions.Options;
using Persistence.Data;

namespace Application.Services.Public.Command
{
    public class SendEmailCommand : IRequest<Unit>
    {
        public SendEmailRequest Request { get; }

        public SendEmailCommand(SendEmailRequest request)
        {
            Request = request;
        }
    }

    public class SendEmailCommandHandler : IRequestHandler<SendEmailCommand, Unit>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly CoworkingSettings _coworkingSettings;

        public SendEmailCommandHandler(
            ApplicationDbContext dbContext,
            IEmailService emailService,
            IOptions<CoworkingSettings> coworkingSettings)
        {
            _dbContext = dbContext;
            _emailService = emailService;
            _coworkingSettings = coworkingSettings.Value;
        }

        public async Task<Unit> Handle(SendEmailCommand command, CancellationToken cancellationToken)
        {
            var request = command.Request;

            var inboxEntry = new Inbox
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                Subject = request.Subject.Trim(),
                Message = request.Message.Trim(),
                Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
                IsRead = false,
                Status = "New"
            };

            _dbContext.Set<Inbox>().Add(inboxEntry);
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (_coworkingSettings.AdminNotificationEmails.Count == 0)
                throw new InvalidOperationException("No admin notification emails are configured.");

            var adminSubject = $"[Contact Us] {inboxEntry.Subject}";
            var adminHtmlContent = BuildAdminNotificationHtml(inboxEntry);

            await _emailService.SendEmailAsync(
                _coworkingSettings.AdminNotificationEmails,
                adminSubject,
                adminHtmlContent);


            var userSubject = "We received your message";
            var userHtmlContent = BuildUserNotificationHtml(inboxEntry);

            await _emailService.SendEmailAsync(
                inboxEntry.Email,
                userSubject,
                userHtmlContent);

            return Unit.Value;
        }

        private static string BuildAdminNotificationHtml(Inbox inboxEntry)
        {
            var fullName = WebUtility.HtmlEncode(inboxEntry.FullName);
            var email = WebUtility.HtmlEncode(inboxEntry.Email);
            var subject = WebUtility.HtmlEncode(inboxEntry.Subject);
            var message = WebUtility.HtmlEncode(inboxEntry.Message).Replace("\n", "<br />");
            var phone = string.IsNullOrWhiteSpace(inboxEntry.Phone)
                ? "N/A"
                : WebUtility.HtmlEncode(inboxEntry.Phone);

            return $"""
                <h2>New Contact Us Message</h2>
                <p>A new public inquiry was submitted through the website.</p>
                <ul>
                    <li><strong>Full Name:</strong> {fullName}</li>
                    <li><strong>Email:</strong> {email}</li>
                    <li><strong>Phone:</strong> {phone}</li>
                    <li><strong>Subject:</strong> {subject}</li>
                    <li><strong>Received At (UTC):</strong> {inboxEntry.CreatedAt:yyyy-MM-dd HH:mm:ss}</li>
                </ul>
                <h3>Message</h3>
                <p>{message}</p>
                """;
        }
        private static string BuildUserNotificationHtml(Inbox inboxEntry)
        {
            var fullName = WebUtility.HtmlEncode(inboxEntry.FullName);
            var subject = WebUtility.HtmlEncode(inboxEntry.Subject);

            return $"""
                <h2>Thanks for contacting us</h2>
                <p>Hi {fullName},</p>
                <p>We received your message and our team will review it shortly.</p>
                <ul>
                    <li><strong>Reference Subject:</strong> {subject}</li>
                    <li><strong>Received At (UTC):</strong> {inboxEntry.CreatedAt:yyyy-MM-dd HH:mm:ss}</li>
                </ul>
                <p>We'll get back to you as soon as possible.</p>
                """;
        }
    }
}
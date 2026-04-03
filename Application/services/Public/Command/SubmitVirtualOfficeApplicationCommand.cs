using System.Net;
using Application.Common.Extensions;
using Application.Common.Exceptions;
using Application.Common.Helpers;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Common.Settings;
using Application.Services.Public.Request;
using Application.Services.Public.Response;
using Domain.Entity.Coworking;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Persistence.Data;
using Serilog;

namespace Application.Services.Public.Command
{
    public class SubmitVirtualOfficeApplicationCommand
        : IRequest<SubmitVirtualOfficeApplicationResponse>
    {
        public SubmitVirtualOfficeApplicationRequest Request { get; }
        public IReadOnlyList<IFormFile> BusinessDocuments { get; }
        public IReadOnlyList<IFormFile> DownPaymentScreenshots { get; }

        public SubmitVirtualOfficeApplicationCommand(
            SubmitVirtualOfficeApplicationRequest request,
            IReadOnlyList<IFormFile> businessDocuments,
            IReadOnlyList<IFormFile> downPaymentScreenshots)
        {
            Request = request;
            BusinessDocuments = businessDocuments;
            DownPaymentScreenshots = downPaymentScreenshots;
        }
    }

    public class SubmitVirtualOfficeApplicationCommandHandler
        : IRequestHandler<SubmitVirtualOfficeApplicationCommand, SubmitVirtualOfficeApplicationResponse>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStorageService _storageService;
        private readonly IEmailService _emailService;
        private readonly CoworkingSettings _settings;

        public SubmitVirtualOfficeApplicationCommandHandler(
            ApplicationDbContext dbContext,
            IStorageService storageService,
            IEmailService emailService,
            IOptions<CoworkingSettings> settings)
        {
            _dbContext = dbContext;
            _storageService = storageService;
            _emailService = emailService;
            _settings = settings.Value;
        }

        public async Task<SubmitVirtualOfficeApplicationResponse> Handle(
            SubmitVirtualOfficeApplicationCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;
            var businessType = BusinessTypeExtensions.ParseBusinessType(req.BusinessType)
                ?? throw new ConflictException("Business type must be sole_proprietorship, partnership, or corporation.");

            // var package = await _dbContext.ServicePackages
            //     .FirstOrDefaultAsync(p => p.Id == req.ServicePackageId && p.IsActive, cancellationToken)
            //     ?? throw new NotFoundException($"Service package '{req.ServicePackageId}' not found or is inactive.");

            // if (package.ResourceType != ResourceType.VirtualOffice)
            //     throw new ConflictException("The selected service package is not a virtual office package.");

            // 1. Read all file bytes up front — streams can only be read once
            var docFiles = await ReadFilesAsync(command.BusinessDocuments, cancellationToken);
            var proofFiles = await ReadFilesAsync(command.DownPaymentScreenshots, cancellationToken);

            var applicationId = Guid.NewGuid();

            // 2. Upload business documents to blob storage
            var docUrls = new List<string>();
            foreach (var file in docFiles)
            {
                var key = FileSecurityHelper.BuildStorageKey(
                    $"virtual-office/{applicationId}/documents", file.FileName);
                var url = await _storageService.UploadFileAsync(
                    key, new MemoryStream(file.Bytes), file.ContentType);
                docUrls.Add(url);
            }

            // 3. Upload down-payment screenshots
            var proofUrls = new List<string>();
            foreach (var file in proofFiles)
            {
                var key = FileSecurityHelper.BuildStorageKey(
                    $"virtual-office/{applicationId}/payment-proofs", file.FileName);
                var url = await _storageService.UploadFileAsync(
                    key, new MemoryStream(file.Bytes), file.ContentType);
                proofUrls.Add(url);
            }

            // 4. Persist application
            var referenceNumber = $"VO-{DateTime.UtcNow:yyyyMMdd}-{applicationId.ToString("N")[..8].ToUpper()}";

            var application = new VirtualOfficeApplication
            {
                Id = applicationId,
                FirstName = req.FirstName.Trim(),
                LastName = req.LastName.Trim(),
                Email = req.Email.Trim(),
                Phone = $"{req.PhoneCountryCode}{req.PhoneNumber}",
                City = req.City.Trim(),
                Country = req.Country.Trim(),
                BusinessType = businessType,
                NeedsBusinessAddress = req.NeedsBusinessAddress,
                NeedsOccasionalSpace = req.NeedsOccasionalSpace,
                DurationMonths = req.DurationMonths,
                ServicePackage = req.ServicePackage.Trim(),
                Status = VirtualOfficeApplicationStatus.Pending,
                BusinessDocumentUrls = docUrls,
                DownPaymentProofUrls = proofUrls,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _dbContext.VirtualOfficeApplications.Add(application);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 5. Send emails with all files attached directly
            var capturedApp = application;
            var capturedRef = referenceNumber;
            _ = Task.Run(async () =>
            {
                try
                {
                    // All business documents + payment proofs go as attachments to admin
                    var adminAttachments = docFiles
                        .Concat(proofFiles)
                        .Select(f => new EmailAttachmentDto
                        {
                            FileName = f.FileName,
                            ContentType = f.ContentType,
                            Content = f.Bytes,
                        })
                        .ToList();

                    await _emailService.SendEmailAsync(
                        _settings.AdminNotificationEmails,
                        $"[Virtual Office Application] {capturedRef} — {capturedApp.FirstName} {capturedApp.LastName}",
                        BuildAdminEmail(capturedApp, capturedRef),
                        adminAttachments);

                    // Customer gets no attachments — just a confirmation message
                    await _emailService.SendEmailAsync(
                        capturedApp.Email,
                        "Virtual Office Application Received",
                        BuildCustomerEmail(capturedApp, capturedRef));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to send emails for virtual office application {ReferenceNumber}",
                        capturedRef);
                }
            }, CancellationToken.None);

            return new SubmitVirtualOfficeApplicationResponse
            {
                ApplicationId = application.Id,
                ReferenceNumber = referenceNumber,
                ApplicantName = $"{application.FirstName} {application.LastName}",
                Message = "Your virtual office application has been received. " +
                                  "Our team will review it and respond within 24 hours.",
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static async Task<List<(string FileName, string ContentType, byte[] Bytes)>> ReadFilesAsync(
            IReadOnlyList<IFormFile> files, CancellationToken cancellationToken)
        {
            var result = new List<(string, string, byte[])>();
            foreach (var file in files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, cancellationToken);
                result.Add((file.FileName, file.ContentType, ms.ToArray()));
            }
            return result;
        }

        // ── Email content ────────────────────────────────────────────────────

        private static string BuildAdminEmail(VirtualOfficeApplication app, string referenceNumber)
        {
            var name = WebUtility.HtmlEncode($"{app.FirstName} {app.LastName}");
            var email = WebUtility.HtmlEncode(app.Email);
            var phone = WebUtility.HtmlEncode(app.Phone);
            var city = WebUtility.HtmlEncode(app.City);
            var country = WebUtility.HtmlEncode(app.Country);
            var businessType = WebUtility.HtmlEncode(app.BusinessType.ToString());
            var bizAddress = app.NeedsBusinessAddress ? "Yes" : "No";
            var occ = app.NeedsOccasionalSpace ? "Yes" : "No";

            return $"""
                <h2>New Virtual Office Application</h2>
                <p>Reference: <strong>{referenceNumber}</strong></p>
                <p>All business documents and payment proof are attached to this email.</p>
                <h3>Applicant Information</h3>
                <ul>
                    <li><strong>Name:</strong> {name}</li>
                    <li><strong>Email:</strong> {email}</li>
                    <li><strong>Phone:</strong> {phone}</li>
                    <li><strong>City:</strong> {city}</li>
                    <li><strong>Country:</strong> {country}</li>
                </ul>
                <h3>Business Details</h3>
                <ul>
                    <li><strong>Business Type:</strong> {businessType}</li>
                    <li><strong>Duration:</strong> {app.DurationMonths} month(s)</li>
                    <li><strong>Needs Business Address (for Permits):</strong> {bizAddress}</li>
                    <li><strong>Needs Occasional Physical Space:</strong> {occ}</li>
                </ul>
                <p>Please review the application and respond to the applicant within 24 hours.</p>
                """;
        }

        private static string BuildCustomerEmail(VirtualOfficeApplication app, string referenceNumber)
        {
            var firstName = WebUtility.HtmlEncode(app.FirstName);

            return $"""
                <h2>Virtual Office Application Received</h2>
                <p>Hi {firstName},</p>
                <p>Thank you for applying for our Virtual Office service. We have received your application
                   and our team is currently reviewing it.</p>
                <ul>
                    <li><strong>Reference Number:</strong> {referenceNumber}</li>
                    <li><strong>Duration Requested:</strong> {app.DurationMonths} month(s)</li>
                    <li><strong>Submitted At (UTC):</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</li>
                </ul>
                <p>You can expect a response from us <strong>within 24 hours</strong> during business days.</p>
                <p>Upon approval, your dashboard login credentials will be sent to this email address.</p>
                <p>If you have any questions in the meantime, feel free to contact us.</p>
                """;
        }
    }
}

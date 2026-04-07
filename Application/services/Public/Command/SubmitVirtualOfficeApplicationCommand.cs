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

            // 4. Calculate pricing
            var price = PriceCalculator.CalculateVirtualOffice(req.ServicePackage, req.DurationMonths);

            // 5. Persist application
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
            var capturedApp        = application;
            var capturedRef        = referenceNumber;
            var capturedDocNames   = docFiles.Select(f => f.FileName).ToList();
            var capturedProofNames = proofFiles.Select(f => f.FileName).ToList();
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
                        $"[New Application] {capturedApp.ServicePackage} — {capturedApp.FirstName} {capturedApp.LastName}",
                        BuildAdminEmail(capturedApp, capturedRef, price),
                        adminAttachments);

                    // Customer gets no attachments — just a confirmation message
                    await _emailService.SendEmailAsync(
                        capturedApp.Email,
                        $"Your Virtual Office Application Has Been Received (Ref: {capturedRef})",
                        BuildCustomerEmail(capturedApp, capturedRef, price, capturedDocNames, capturedProofNames));
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

        private static string BuildAdminEmail(
            VirtualOfficeApplication app, string referenceNumber,
            PriceCalculator.VirtualOfficePriceResult price)
        {
            var name           = WebUtility.HtmlEncode($"{app.FirstName} {app.LastName}");
            var email          = WebUtility.HtmlEncode(app.Email);
            var phone          = WebUtility.HtmlEncode(app.Phone);
            var city           = WebUtility.HtmlEncode(app.City);
            var country        = WebUtility.HtmlEncode(app.Country);
            var servicePackage = WebUtility.HtmlEncode(app.ServicePackage);
            var businessType   = WebUtility.HtmlEncode(app.BusinessType.ToString());
            var bizAddress     = app.NeedsBusinessAddress ? "Yes" : "No";
            var occ            = app.NeedsOccasionalSpace ? "Yes" : "No";

            const string TdL     = "padding:10px 24px 10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdV     = "padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdLLast = "padding:10px 24px 10px 0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdVLast = "padding:10px 0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";

            return $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width,initial-scale=1.0" />
                  <link href="https://fonts.googleapis.com/css2?family=Righteous&amp;display=swap" rel="stylesheet" />
                  <style>
                    @import url('https://fonts.googleapis.com/css2?family=Righteous&display=swap');
                    @media only screen and (max-width:600px) {
                      .ew-container { width:100% !important; border-radius:0 !important; }
                      .ew-hdr { padding:28px 20px !important; }
                      .ew-body { padding:28px 20px !important; }
                      .ew-ftr { padding:20px !important; }
                      .ew-h1 { font-size:22px !important; }
                    }
                  </style>
                </head>
                <body style="margin:0;padding:0;background-color:#f0f0f8;font-family:Arial,Helvetica,sans-serif;">

                  <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f0f0f8;padding:28px 0;">
                    <tr>
                      <td align="center" style="padding:0 12px;">
                        <table class="ew-container" cellpadding="0" cellspacing="0" border="0"
                               style="width:600px;max-width:600px;background-color:#ffffff;border-radius:12px;overflow:hidden;">

                          <!-- ── Header ───────────────────────────────────────── -->
                          <tr>
                            <td class="ew-hdr" style="background-color:#0C008A;padding:36px 40px;text-align:center;">
                              <h1 class="ew-h1" style="margin:0;font-family:'Righteous',Arial,sans-serif;font-size:28px;letter-spacing:3px;line-height:1.2;">
                                <span style="color:#ffffff;">COMMUN</span><span style="color:#FF7700;">.</span><span style="color:#ffffff;">SIDE</span>
                              </h1>
                              <p style="margin:8px 0 0;color:rgba(255,255,255,0.70);font-size:13px;font-family:Arial,sans-serif;">Admin Notification</p>
                              <div style="display:inline-block;background-color:#FF7700;color:#ffffff;padding:7px 20px;border-radius:20px;font-size:11px;font-weight:bold;letter-spacing:1.5px;margin-top:16px;text-transform:uppercase;">
                                New Virtual Office Application
                              </div>
                            </td>
                          </tr>

                          <!-- ── Body ─────────────────────────────────────────── -->
                          <tr>
                            <td class="ew-body" style="padding:36px 40px;background-color:#ffffff;">

                              <!-- Reference Number -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#0C008A;border-radius:10px;margin-bottom:28px;">
                                <tr>
                                  <td style="padding:20px 24px;">
                                    <p style="margin:0 0 4px;color:rgba(255,255,255,0.60);font-size:10px;letter-spacing:2.5px;text-transform:uppercase;font-family:Arial,sans-serif;">Reference Number</p>
                                    <p style="margin:0;color:#FF7700;font-family:'Righteous',monospace;font-size:22px;letter-spacing:1px;">{{referenceNumber}}</p>
                                  </td>
                                </tr>
                              </table>

                              <!-- Selected Package -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Selected Package</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:4px;">
                                <tr>
                                  <td style="{{TdL}}">Package</td>
                                  <td style="{{TdV}}">{{servicePackage}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Duration</td>
                                  <td style="{{TdVLast}}">{{app.DurationMonths}} month(s)</td>
                                </tr>
                              </table>

                              <!-- Pricing -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Pricing Breakdown</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td style="{{TdL}}">Monthly Rate</td>
                                  <td style="{{TdV}}">&#8369;{{price.MonthlyRate:N2}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">Duration</td>
                                  <td style="{{TdV}}">{{price.DurationMonths}} month(s)</td>
                                </tr>
                                <tr>
                                  <td style="padding:12px 24px 12px 0;background-color:#f0eeff;font-size:14px;color:#888888;width:40%;font-family:Arial,sans-serif;">Total Amount</td>
                                  <td style="padding:12px 0;background-color:#f0eeff;font-size:18px;color:#FF7700;font-weight:bold;font-family:Arial,sans-serif;">&#8369;{{price.TotalAmount:N2}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Downpayment Required (50%)</td>
                                  <td style="{{TdVLast}}">&#8369;{{price.DownpaymentAmount:N2}}</td>
                                </tr>
                              </table>

                              <!-- Divider -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:24px 0;">
                                <tr><td style="border-top:1px solid #eeeeee;font-size:0;line-height:0;">&nbsp;</td></tr>
                              </table>

                              <!-- Applicant Information -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Applicant Information</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td style="{{TdL}}">Name</td>
                                  <td style="{{TdV}}">{{name}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">Email</td>
                                  <td style="{{TdV}}">{{email}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">Phone</td>
                                  <td style="{{TdV}}">{{phone}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">City</td>
                                  <td style="{{TdV}}">{{city}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Country</td>
                                  <td style="{{TdVLast}}">{{country}}</td>
                                </tr>
                              </table>

                              <!-- Business Details -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Business Details</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td style="{{TdL}}">Business Type</td>
                                  <td style="{{TdV}}">{{businessType}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">Needs Business Address</td>
                                  <td style="{{TdV}}">{{bizAddress}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Needs Occasional Space</td>
                                  <td style="{{TdVLast}}">{{occ}}</td>
                                </tr>
                              </table>

                              <!-- Action Note -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:28px;background-color:#fff8f0;border-left:4px solid #FF7700;border-radius:0 8px 8px 0;">
                                <tr>
                                  <td style="padding:16px 20px;">
                                    <p style="margin:0;font-size:14px;color:#555555;font-family:Arial,sans-serif;">
                                      &#128196;&nbsp; All business documents and payment proofs are <strong>attached</strong> to this email. Please review and respond within 24 hours.
                                    </p>
                                  </td>
                                </tr>
                              </table>

                            </td>
                          </tr>

                          <!-- ── Brand strip ──────────────────────────────────── -->
                          <tr>
                            <td style="background-color:#ffffff;padding:28px 40px;text-align:center;border-top:1px solid #eeeeee;">
                              <h2 style="margin:0;font-family:'Righteous',Arial,sans-serif;font-size:22px;letter-spacing:3px;line-height:1;">
                                <span style="color:#0C008A;">COMMUN</span><span style="color:#FF7700;">.</span><span style="color:#0C008A;">SIDE</span>
                              </h2>
                            </td>
                          </tr>

                          <!-- ── Copyright footer ──────────────────────────────── -->
                          <tr>
                            <td class="ew-ftr" style="background-color:#0C008A;padding:20px 40px;text-align:center;">
                              <p style="margin:0;color:rgba(255,255,255,0.55);font-size:12px;font-family:Arial,sans-serif;line-height:1.8;">&#169; 2026 Commun.Side &middot; All rights reserved</p>
                              <p style="margin:4px 0 0;color:rgba(255,255,255,0.35);font-size:11px;font-family:Arial,sans-serif;">This is an automated message. Please do not reply directly to this email.</p>
                            </td>
                          </tr>

                        </table>
                      </td>
                    </tr>
                  </table>

                </body>
                </html>
                """;
        }

        private static string BuildCustomerEmail(
            VirtualOfficeApplication app, string referenceNumber,
            PriceCalculator.VirtualOfficePriceResult price,
            List<string> docFileNames, List<string> proofFileNames)
        {
            var firstName      = WebUtility.HtmlEncode(app.FirstName);
            var servicePackage = WebUtility.HtmlEncode(app.ServicePackage);
            var phTime         = DateTime.UtcNow.AddHours(8);
            var submittedAt    = phTime.ToString("MMMM d, yyyy h:mm tt") + " PHT";

            const string TdL     = "padding:10px 24px 10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdV     = "padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdLLast = "padding:10px 24px 10px 0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdVLast = "padding:10px 0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";

            var docRows = string.Join("", docFileNames.Select(fn =>
                $"<tr><td colspan=\"2\" style=\"padding:8px 0;border-bottom:1px solid #f0f0f0;font-size:13px;color:#555555;font-family:Arial,sans-serif;\">&#128196;&nbsp; {WebUtility.HtmlEncode(fn)}</td></tr>"));
            var proofRows = string.Join("", proofFileNames.Select(fn =>
                $"<tr><td colspan=\"2\" style=\"padding:8px 0;border-bottom:1px solid #f0f0f0;font-size:13px;color:#555555;font-family:Arial,sans-serif;\">&#128247;&nbsp; {WebUtility.HtmlEncode(fn)}</td></tr>"));

            return $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width,initial-scale=1.0" />
                  <link href="https://fonts.googleapis.com/css2?family=Righteous&amp;display=swap" rel="stylesheet" />
                  <style>
                    @import url('https://fonts.googleapis.com/css2?family=Righteous&display=swap');
                    @media only screen and (max-width:600px) {
                      .ew-container { width:100% !important; border-radius:0 !important; }
                      .ew-hdr { padding:28px 20px !important; }
                      .ew-body { padding:28px 20px !important; }
                      .ew-ftr { padding:20px !important; }
                      .ew-h1 { font-size:22px !important; }
                    }
                  </style>
                </head>
                <body style="margin:0;padding:0;background-color:#f0f0f8;font-family:Arial,Helvetica,sans-serif;">

                  <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f0f0f8;padding:28px 0;">
                    <tr>
                      <td align="center" style="padding:0 12px;">
                        <table class="ew-container" cellpadding="0" cellspacing="0" border="0"
                               style="width:600px;max-width:600px;background-color:#ffffff;border-radius:12px;overflow:hidden;">

                          <!-- ── Header ───────────────────────────────────────── -->
                          <tr>
                            <td class="ew-hdr" style="background-color:#0C008A;padding:36px 40px;text-align:center;">
                              <h1 class="ew-h1" style="margin:0;font-family:'Righteous',Arial,sans-serif;font-size:28px;letter-spacing:3px;line-height:1.2;">
                                <span style="color:#ffffff;">COMMUN</span><span style="color:#FF7700;">.</span><span style="color:#ffffff;">SIDE</span>
                              </h1>
                              <p style="margin:8px 0 0;color:rgba(255,255,255,0.70);font-size:13px;font-family:Arial,sans-serif;">
                                Your coworking space, your way.
                              </p>
                              <div style="display:inline-block;background-color:#FF7700;color:#ffffff;padding:7px 20px;border-radius:20px;font-size:11px;font-weight:bold;letter-spacing:1.5px;margin-top:16px;text-transform:uppercase;">
                                Application Received
                              </div>
                            </td>
                          </tr>

                          <!-- ── Body ─────────────────────────────────────────── -->
                          <tr>
                            <td class="ew-body" style="padding:36px 40px;background-color:#ffffff;">

                              <p style="margin:0 0 6px;font-size:22px;color:#0C008A;font-weight:bold;font-family:Arial,sans-serif;">Hi {{firstName}}!</p>
                              <p style="margin:0 0 28px;font-size:15px;color:#555555;line-height:1.7;font-family:Arial,sans-serif;">
                                Thank you for applying for our Virtual Office service. We have received your application
                                and our team is currently reviewing it. You can expect a response
                                <strong>within 24 hours</strong> during business days.
                              </p>

                              <!-- Reference Number -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#0C008A;border-radius:10px;margin-bottom:28px;">
                                <tr>
                                  <td style="padding:20px 24px;">
                                    <p style="margin:0 0 4px;color:rgba(255,255,255,0.60);font-size:10px;letter-spacing:2.5px;text-transform:uppercase;font-family:Arial,sans-serif;">Reference Number</p>
                                    <p style="margin:0;color:#FF7700;font-family:'Righteous',monospace;font-size:22px;letter-spacing:1px;">{{referenceNumber}}</p>
                                  </td>
                                </tr>
                              </table>

                              <!-- Application Summary -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Application Summary</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:4px;">
                                <tr>
                                  <td style="{{TdL}}">Selected Package</td>
                                  <td style="{{TdV}}">{{servicePackage}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">Duration</td>
                                  <td style="{{TdV}}">{{app.DurationMonths}} month(s)</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Submitted At</td>
                                  <td style="{{TdVLast}}">{{submittedAt}}</td>
                                </tr>
                              </table>

                              <!-- Pricing Summary -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Pricing Summary</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td style="{{TdL}}">Monthly Rate</td>
                                  <td style="{{TdV}}">&#8369;{{price.MonthlyRate:N2}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdL}}">Duration</td>
                                  <td style="{{TdV}}">{{price.DurationMonths}} month(s)</td>
                                </tr>
                                <tr>
                                  <td style="padding:12px 24px 12px 0;background-color:#f0eeff;font-size:14px;color:#888888;width:40%;font-family:Arial,sans-serif;">Total Amount</td>
                                  <td style="padding:12px 0;background-color:#f0eeff;font-size:18px;color:#FF7700;font-weight:bold;font-family:Arial,sans-serif;">&#8369;{{price.TotalAmount:N2}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Downpayment Required (50%)</td>
                                  <td style="{{TdVLast}}">&#8369;{{price.DownpaymentAmount:N2}}</td>
                                </tr>
                              </table>

                              <!-- Submitted Documents -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Documents You Submitted</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                {{docRows}}
                                {{proofRows}}
                              </table>

                              <!-- Divider -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:28px 0;">
                                <tr><td style="border-top:1px solid #eeeeee;font-size:0;line-height:0;">&nbsp;</td></tr>
                              </table>

                              <!-- What's Next -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">What Happens Next?</p>
                              <p style="margin:0;font-size:14px;color:#555555;line-height:2.4;font-family:Arial,sans-serif;">
                                &#9989;&nbsp; Application submitted successfully<br />
                                &#9203;&nbsp; Our team is reviewing your documents<br />
                                &#128231;&nbsp; Approval decision within 24 business hours<br />
                                &#128274;&nbsp; Dashboard credentials sent upon approval
                              </p>

                              <!-- Contact Box -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:28px;background-color:#f8f7ff;border-left:4px solid #FF7700;border-radius:0 8px 8px 0;">
                                <tr>
                                  <td style="padding:20px 24px;">
                                    <p style="margin:0 0 8px;font-size:15px;font-weight:bold;color:#0C008A;font-family:Arial,sans-serif;">Have questions about your application?</p>
                                    <p style="margin:0 0 10px;font-size:14px;color:#555555;font-family:Arial,sans-serif;">We're happy to help! Reach us through:</p>
                                    <p style="margin:0 0 6px;font-size:14px;color:#555555;font-family:Arial,sans-serif;">
                                      &#128231;&nbsp; <a href="mailto:communside@gmail.com" style="color:#0C008A;font-weight:bold;text-decoration:none;">communside@gmail.com</a>
                                    </p>
                                    <p style="margin:0;font-size:14px;color:#555555;font-family:Arial,sans-serif;">
                                      &#128222;&nbsp; <span style="color:#FF7700;font-weight:bold;">+63 912 345 6789</span>
                                    </p>
                                  </td>
                                </tr>
                              </table>

                            </td>
                          </tr>

                          <!-- ── Brand strip ──────────────────────────────────── -->
                          <tr>
                            <td style="background-color:#ffffff;padding:28px 40px;text-align:center;border-top:1px solid #eeeeee;">
                              <h2 style="margin:0;font-family:'Righteous',Arial,sans-serif;font-size:22px;letter-spacing:3px;line-height:1;">
                                <span style="color:#0C008A;">COMMUN</span><span style="color:#FF7700;">.</span><span style="color:#0C008A;">SIDE</span>
                              </h2>
                            </td>
                          </tr>

                          <!-- ── Copyright footer ──────────────────────────────── -->
                          <tr>
                            <td class="ew-ftr" style="background-color:#0C008A;padding:20px 40px;text-align:center;">
                              <p style="margin:0;color:rgba(255,255,255,0.55);font-size:12px;font-family:Arial,sans-serif;line-height:1.8;">&#169; 2026 Commun.Side &middot; All rights reserved</p>
                              <p style="margin:4px 0 0;color:rgba(255,255,255,0.35);font-size:11px;font-family:Arial,sans-serif;">This is an automated message. Please do not reply directly to this email.</p>
                            </td>
                          </tr>

                        </table>
                      </td>
                    </tr>
                  </table>

                </body>
                </html>
                """;
        }
    }
}

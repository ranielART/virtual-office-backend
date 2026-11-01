using System.Net;
using Application.Common.Exceptions;
using Application.Common.Helpers;
using Application.Common.Interfaces;
using Application.Common.Models;
using Application.Common.Settings;
using Application.Services.Public.Request;
using Application.Services.Public.Response;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Serilog;

namespace Application.Services.Public.Command
{
    public class SubmitCoworkingBookingCommand : IRequest<SubmitCoworkingBookingResponse>
    {
        public SubmitCoworkingBookingRequest Request { get; }
        public IReadOnlyList<IFormFile> PaymentScreenshots { get; }
        public IReadOnlyList<IFormFile> StudentIdScreenshots { get; }

        public SubmitCoworkingBookingCommand(
            SubmitCoworkingBookingRequest request,
            IReadOnlyList<IFormFile> paymentScreenshots,
            IReadOnlyList<IFormFile> studentIdScreenshots)
        {
            Request = request;
            PaymentScreenshots = paymentScreenshots;
            StudentIdScreenshots = studentIdScreenshots;
        }
    }

    public class SubmitCoworkingBookingCommandHandler
        : IRequestHandler<SubmitCoworkingBookingCommand, SubmitCoworkingBookingResponse>
    {
        private readonly IStorageService _storageService;
        private readonly IEmailService _emailService;
        private readonly CoworkingSettings _settings;

        public SubmitCoworkingBookingCommandHandler(
            IStorageService storageService,
            IEmailService emailService,
            IOptions<CoworkingSettings> settings)
        {
            _storageService = storageService;
            _emailService   = emailService;
            _settings       = settings.Value;
        }

        public async Task<SubmitCoworkingBookingResponse> Handle(
            SubmitCoworkingBookingCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            // 1. Compute hours (only needed for hourly packages)
            var isHourly = PriceCalculator.IsHourlyCoworkingPackage(req.ResourcePackage);
            decimal hours = 0m;
            if (isHourly)
            {
                if (!req.EndTime.HasValue)
                    throw new ConflictException("End time is required for hourly packages.");
                if (req.EndTime.Value <= req.StartTime)
                    throw new ConflictException("End time must be after start time.");

                hours = (decimal)(req.EndTime.Value - req.StartTime).TotalHours;
                if (hours <= 0 || hours > 24)
                    throw new ConflictException("Booking duration must be between 0 and 24 hours.");
            }

            // 2. Calculate pricing — no DB lookup needed
            var price = PriceCalculator.CalculateCoworking(req.ResourcePackage, req.IsStudent, hours);

            // 3. Generate reference number
            var inquiryId        = Guid.NewGuid();
            var referenceNumber  = $"BKG-{DateTime.UtcNow:yyyyMMdd}-{inquiryId.ToString("N")[..8].ToUpper()}";

            // 4. Read all file bytes up front — streams can only be read once
            var screenshotFiles   = await ReadFilesAsync(command.PaymentScreenshots, cancellationToken);
            var studentIdFiles    = await ReadFilesAsync(command.StudentIdScreenshots, cancellationToken);

            // 5. Upload screenshots to blob (for admin record-keeping)
            var proofUrls = new List<string>();
            foreach (var file in screenshotFiles)
            {
                var key = FileSecurityHelper.BuildStorageKey(
                    $"coworking-inquiries/{inquiryId}/payment-proofs", file.FileName);
                var url = await _storageService.UploadFileAsync(
                    key, new MemoryStream(file.Bytes), file.ContentType);
                proofUrls.Add(url);
            }

            foreach (var file in studentIdFiles)
            {
                var key = FileSecurityHelper.BuildStorageKey(
                    $"coworking-inquiries/{inquiryId}/student-ids", file.FileName);
                await _storageService.UploadFileAsync(
                    key, new MemoryStream(file.Bytes), file.ContentType);
            }

            // 6. Send emails — fire-and-forget
            var guestName               = $"{req.FirstName.Trim()} {req.LastName.Trim()}";
            var capturedScreenshotNames = screenshotFiles.Select(f => f.FileName).ToList();
            var capturedStudentIdNames  = studentIdFiles.Select(f => f.FileName).ToList();
            _ = Task.Run(async () =>
            {
                try
                {
                    var paymentAttachments = screenshotFiles
                        .Select(f => new EmailAttachmentDto
                        {
                            FileName    = f.FileName,
                            ContentType = f.ContentType,
                            Content     = f.Bytes,
                        })
                        .ToList();

                    var studentIdAttachments = studentIdFiles
                        .Select(f => new EmailAttachmentDto
                        {
                            FileName    = f.FileName,
                            ContentType = f.ContentType,
                            Content     = f.Bytes,
                        })
                        .ToList();

                    var allAttachments = paymentAttachments.Concat(studentIdAttachments).ToList();

                    await _emailService.SendEmailAsync(
                        _settings.AdminNotificationEmails,
                        $"[New Booking] {req.ResourcePackage} — {guestName}",
                        BuildAdminEmail(req, referenceNumber, guestName, price),
                        allAttachments);

                    await _emailService.SendEmailAsync(
                        req.Email.Trim(),
                        $"Your Booking Inquiry Has Been Received (Ref: {referenceNumber})",
                        BuildCustomerEmail(req, referenceNumber, guestName, price, capturedScreenshotNames, capturedStudentIdNames),
                        allAttachments);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to send emails for coworking booking inquiry {ReferenceNumber}",
                        referenceNumber);
                }
            }, CancellationToken.None);

            return new SubmitCoworkingBookingResponse
            {
                ReferenceNumber   = referenceNumber,
                GuestName         = guestName,
                ResourcePackage   = req.ResourcePackage,
                Date              = req.Date,
                StartTime         = req.StartTime,
                EndTime           = req.EndTime,
                Hours             = price.Hours,
                BaseAmount        = price.BaseAmount,
                DiscountAmount    = price.DiscountAmount,
                TotalAmount       = price.TotalAmount,
                DownpaymentAmount = price.DownpaymentAmount,
                IsStudent         = req.IsStudent,
                Message           = "Your booking inquiry has been received. We will confirm it once payment has been verified.",
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
            SubmitCoworkingBookingRequest req,
            string referenceNumber,
            string guestName,
            PriceCalculator.CoworkingPriceResult price)
        {
            var name      = WebUtility.HtmlEncode(guestName);
            var email     = WebUtility.HtmlEncode(req.Email.Trim());
            var phone     = WebUtility.HtmlEncode($"{req.PhoneCountryCode}{req.PhoneNumber}");
            var rp        = WebUtility.HtmlEncode(req.ResourcePackage);
            var isStudent = req.IsStudent ? "Yes" : "No";
            var dateStr   = req.Date.ToString("MMMM d, yyyy");
            var startStr  = req.StartTime.ToString("h:mm tt");

            const string TdL     = "padding:10px 24px 10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdV     = "padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdG     = "padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#22a84a;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdLLast = "padding:10px 24px 10px 0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdVLast = "padding:10px 0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";

            string scheduleRows;
            if (price.IsHourly && req.EndTime.HasValue)
            {
                var endStr = req.EndTime.Value.ToString("h:mm tt");
                scheduleRows =
                    $"<tr><td style=\"{TdL}\">Date</td><td style=\"{TdV}\">{dateStr}</td></tr>" +
                    $"<tr><td style=\"{TdL}\">Start Time</td><td style=\"{TdV}\">{startStr}</td></tr>" +
                    $"<tr><td style=\"{TdL}\">End Time</td><td style=\"{TdV}\">{endStr}</td></tr>" +
                    $"<tr><td style=\"{TdL}\">Duration</td><td style=\"{TdV}\">{price.Hours:0.##} hour(s)</td></tr>" +
                    $"<tr><td style=\"{TdLLast}\">Student</td><td style=\"{TdVLast}\">{isStudent}</td></tr>";
            }
            else
            {
                scheduleRows =
                    $"<tr><td style=\"{TdL}\">Date</td><td style=\"{TdV}\">{dateStr}</td></tr>" +
                    $"<tr><td style=\"{TdL}\">Start Time</td><td style=\"{TdV}\">{startStr}</td></tr>" +
                    $"<tr><td style=\"{TdLLast}\">Student</td><td style=\"{TdVLast}\">{isStudent}</td></tr>";
            }

            string discountRow;
            if (price.DiscountAmount > 0)
                discountRow = $"<tr><td style=\"{TdL}\">Discount ({WebUtility.HtmlEncode(price.DiscountLabel)})</td><td style=\"{TdG}\">-&#8369;{price.DiscountAmount:N2}</td></tr>";
            else if (price.IsStudent && price.IsHourly)
                discountRow = $"<tr><td style=\"{TdL}\">Rate Applied</td><td style=\"{TdV}\">{WebUtility.HtmlEncode(price.DiscountLabel)}</td></tr>";
            else
                discountRow = $"<tr><td style=\"{TdL}\">Discount</td><td style=\"{TdV}\">None</td></tr>";

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
                                Admin Notification
                              </p>
                              <div style="display:inline-block;background-color:#FF7700;color:#ffffff;padding:7px 20px;border-radius:20px;font-size:11px;font-weight:bold;letter-spacing:1.5px;margin-top:16px;text-transform:uppercase;">
                                New Booking Inquiry
                              </div>
                            </td>
                          </tr>

                          <!-- ── Body ─────────────────────────────────────────── -->
                          <tr>
                            <td class="ew-body" style="padding:36px 40px;background-color:#ffffff;">

                              <!-- Reference Number -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0"
                                     style="background-color:#0C008A;border-radius:10px;margin-bottom:28px;">
                                <tr>
                                  <td style="padding:20px 24px;">
                                    <p style="margin:0 0 4px;color:rgba(255,255,255,0.60);font-size:10px;letter-spacing:2.5px;text-transform:uppercase;font-family:Arial,sans-serif;">Reference Number</p>
                                    <p style="margin:0;color:#FF7700;font-family:'Righteous',monospace;font-size:22px;letter-spacing:1px;">{{referenceNumber}}</p>
                                  </td>
                                </tr>
                              </table>

                              <!-- Booking Details -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Booking Details</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:4px;">
                                <tr>
                                  <td style="{{TdL}}">Package</td>
                                  <td style="{{TdV}}">{{rp}}</td>
                                </tr>
                                {{scheduleRows}}
                              </table>

                              <!-- Pricing -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Pricing Breakdown</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td style="{{TdL}}">Base Amount</td>
                                  <td style="{{TdV}}">&#8369;{{price.BaseAmount:N2}}</td>
                                </tr>
                                {{discountRow}}
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

                              <!-- Guest Information -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Guest Information</p>
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
                                  <td style="{{TdLLast}}">Phone</td>
                                  <td style="{{TdVLast}}">{{phone}}</td>
                                </tr>
                              </table>

                              <!-- Action Note -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0"
                                     style="margin-top:28px;background-color:#fff8f0;border-left:4px solid #FF7700;border-radius:0 8px 8px 0;">
                                <tr>
                                  <td style="padding:16px 20px;">
                                    <p style="margin:0;font-size:14px;color:#555555;font-family:Arial,sans-serif;">
                                      &#128247;&nbsp; Payment screenshot(s) are <strong>attached</strong> to this email. Please verify and confirm the booking.
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
            SubmitCoworkingBookingRequest req,
            string referenceNumber,
            string guestName,
            PriceCalculator.CoworkingPriceResult price,
            List<string> screenshotFileNames,
            List<string> studentIdFileNames)
        {
            var firstName = WebUtility.HtmlEncode(req.FirstName.Trim());
            var rp        = WebUtility.HtmlEncode(req.ResourcePackage);
            var dateStr   = req.Date.ToString("MMMM d, yyyy");
            var startStr  = req.StartTime.ToString("h:mm tt");

            const string TdL     = "padding:10px 24px 10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdV     = "padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdG     = "padding:10px 0;border-bottom:1px solid #f0f0f0;font-size:14px;color:#22a84a;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdLLast = "padding:10px 24px 10px 0;font-size:14px;color:#888888;width:40%;vertical-align:top;font-family:Arial,sans-serif;";
            const string TdVLast = "padding:10px 0;font-size:14px;color:#0C008A;font-weight:600;vertical-align:top;font-family:Arial,sans-serif;";

            string scheduleRows;
            if (price.IsHourly && req.EndTime.HasValue)
            {
                var endStr = req.EndTime.Value.ToString("h:mm tt");
                scheduleRows =
                    $"<tr><td style=\"{TdL}\">Date</td><td style=\"{TdV}\">{dateStr}</td></tr>" +
                    $"<tr><td style=\"{TdL}\">Start Time</td><td style=\"{TdV}\">{startStr}</td></tr>" +
                    $"<tr><td style=\"{TdL}\">End Time</td><td style=\"{TdV}\">{endStr}</td></tr>" +
                    $"<tr><td style=\"{TdLLast}\">Duration</td><td style=\"{TdVLast}\">{price.Hours:0.##} hour(s)</td></tr>";
            }
            else
            {
                scheduleRows =
                    $"<tr><td style=\"{TdL}\">Date</td><td style=\"{TdV}\">{dateStr}</td></tr>" +
                    $"<tr><td style=\"{TdLLast}\">Start Time</td><td style=\"{TdVLast}\">{startStr}</td></tr>";
            }

            string discountRow;
            if (price.DiscountAmount > 0)
                discountRow = $"<tr><td style=\"{TdL}\">Discount ({WebUtility.HtmlEncode(price.DiscountLabel)})</td><td style=\"{TdG}\">-&#8369;{price.DiscountAmount:N2}</td></tr>";
            else if (price.IsStudent && price.IsHourly)
                discountRow = $"<tr><td style=\"{TdL}\">Rate Applied</td><td style=\"{TdV}\">{WebUtility.HtmlEncode(price.DiscountLabel)}</td></tr>";
            else
                discountRow = string.Empty;

            var screenshotRows = string.Join("", screenshotFileNames.Select(fn =>
                $"<tr><td colspan=\"2\" style=\"padding:8px 0;border-bottom:1px solid #f0f0f0;font-size:13px;color:#555555;font-family:Arial,sans-serif;\">&#128247;&nbsp; {WebUtility.HtmlEncode(fn)}</td></tr>"));

            var studentIdSection = studentIdFileNames.Count > 0
                ? $"""
                              <!-- Student ID Screenshots -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Student ID You Submitted</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                {string.Join("", studentIdFileNames.Select(fn =>
                                    $"<tr><td colspan=\"2\" style=\"padding:8px 0;border-bottom:1px solid #f0f0f0;font-size:13px;color:#555555;font-family:Arial,sans-serif;\">&#127891;&nbsp; {WebUtility.HtmlEncode(fn)}</td></tr>"))}
                              </table>
                  """
                : string.Empty;

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
                                Booking Inquiry Received
                              </div>
                            </td>
                          </tr>

                          <!-- ── Body ─────────────────────────────────────────── -->
                          <tr>
                            <td class="ew-body" style="padding:36px 40px;background-color:#ffffff;">

                              <p style="margin:0 0 6px;font-size:22px;color:#0C008A;font-weight:bold;font-family:Arial,sans-serif;">Hi {{firstName}}!</p>
                              <p style="margin:0 0 28px;font-size:15px;color:#555555;line-height:1.7;font-family:Arial,sans-serif;">
                                Thank you for choosing us. Your booking inquiry has been submitted and is now
                                <strong>pending payment verification</strong>. We'll confirm your spot once we've reviewed your payment.
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

                              <!-- Booking Details -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Booking Details</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:4px;">
                                <tr>
                                  <td style="{{TdL}}">Package</td>
                                  <td style="{{TdV}}">{{rp}}</td>
                                </tr>
                                {{scheduleRows}}
                              </table>

                              <!-- Pricing Breakdown -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Pricing Breakdown</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                  <td style="{{TdL}}">Base Amount</td>
                                  <td style="{{TdV}}">&#8369;{{price.BaseAmount:N2}}</td>
                                </tr>
                                {{discountRow}}
                                <tr>
                                  <td style="padding:12px 24px 12px 0;background-color:#f0eeff;font-size:14px;color:#888888;width:40%;font-family:Arial,sans-serif;">Total Amount</td>
                                  <td style="padding:12px 0;background-color:#f0eeff;font-size:18px;color:#FF7700;font-weight:bold;font-family:Arial,sans-serif;">&#8369;{{price.TotalAmount:N2}}</td>
                                </tr>
                                <tr>
                                  <td style="{{TdLLast}}">Downpayment Required (50%)</td>
                                  <td style="{{TdVLast}}">&#8369;{{price.DownpaymentAmount:N2}}</td>
                                </tr>
                              </table>

                              <!-- Submitted Screenshots -->
                              <p style="margin:24px 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">Payment Screenshots You Submitted</p>
                              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                {{screenshotRows}}
                              </table>

                              {{studentIdSection}}

                              <!-- Divider -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:28px 0;">
                                <tr><td style="border-top:1px solid #eeeeee;font-size:0;line-height:0;">&nbsp;</td></tr>
                              </table>

                              <!-- What's Next -->
                              <p style="margin:0 0 10px;font-family:'Righteous',Arial,sans-serif;color:#0C008A;font-size:12px;letter-spacing:2px;text-transform:uppercase;border-bottom:2px solid #FF7700;padding-bottom:6px;">What Happens Next?</p>
                              <p style="margin:0;font-size:14px;color:#555555;line-height:2.4;font-family:Arial,sans-serif;">
                                &#9989;&nbsp; Payment screenshot submitted<br />
                                &#9203;&nbsp; Our team is reviewing your payment<br />
                                &#128231;&nbsp; You'll receive a confirmation once verified
                              </p>
                              <p style="font-size:13px;color:#aaaaaa;margin:10px 0 0;font-family:Arial,sans-serif;">This typically takes a few hours during business hours.</p>

                              <!-- Contact Box -->
                              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-top:28px;background-color:#f8f7ff;border-left:4px solid #FF7700;border-radius:0 8px 8px 0;">
                                <tr>
                                  <td style="padding:20px 24px;">
                                    <p style="margin:0 0 8px;font-size:15px;font-weight:bold;color:#0C008A;font-family:Arial,sans-serif;">Have questions about your booking?</p>
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

using System.ComponentModel;
using System.Net;
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
    public class SubmitCoworkingBookingCommand : IRequest<SubmitCoworkingBookingResponse>
    {
        public SubmitCoworkingBookingRequest Request { get; }
        public IReadOnlyList<IFormFile> PaymentScreenshots { get; }

        public SubmitCoworkingBookingCommand(
            SubmitCoworkingBookingRequest request,
            IReadOnlyList<IFormFile> paymentScreenshots)
        {
            Request = request;
            PaymentScreenshots = paymentScreenshots;
        }
    }

    public class SubmitCoworkingBookingCommandHandler
        : IRequestHandler<SubmitCoworkingBookingCommand, SubmitCoworkingBookingResponse>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IStorageService _storageService;
        private readonly IEmailService _emailService;
        private readonly CoworkingSettings _settings;

        public SubmitCoworkingBookingCommandHandler(
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

        public async Task<SubmitCoworkingBookingResponse> Handle(
            SubmitCoworkingBookingCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            // 1. Load package
            var package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == req.ServicePackageId && p.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Service package '{req.ServicePackageId}' not found or is inactive.");

            // 2. Resolve schedule (UTC)
            var scheduledStart = req.Date.ToDateTime(req.StartTime, DateTimeKind.Utc);
            var scheduledEnd = ResolveScheduledEnd(package, req.StartTime, req.EndTime, req.Date);

            if (scheduledEnd <= scheduledStart)
                throw new ConflictException("End time must be after start time.");

            // 3. Pricing
            var estimatedHours = (decimal)(scheduledEnd - scheduledStart).TotalHours;

            var baseAmount = package.PackageType == PackageType.Hourly
                ? estimatedHours * package.PriceAmount
                : package.PriceAmount;

            var discountAmount = req.IsStudent && package.IsStudentEligible
                ? baseAmount * (package.StudentDiscountPercent / 100m)
                : 0m;

            var totalAmount = baseAmount - discountAmount;

            decimal snapshotHourlyRate = package.PackageType == PackageType.Hourly
                ? package.PriceAmount
                : package.DurationHours.HasValue
                    ? package.PriceAmount / package.DurationHours.Value
                    : package.DurationDays.HasValue
                        ? package.PriceAmount / (package.DurationDays.Value * 8m)
                        : package.PriceAmount;

            // 4. Read all file bytes up front — streams can only be read once
            var screenshotFiles = await ReadFilesAsync(command.PaymentScreenshots, cancellationToken);

            // 5. Begin transaction — find resource + create booking atomically
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            Booking booking;
            BookingGuest guest;
            try
            {
                // Find any available resource of the required type
                var resource = await _dbContext.CoworkingResources
                    .Where(r => r.Id == req.ResourceId && r.IsActive)
                    .FirstOrDefaultAsync(cancellationToken)
                    ?? throw new ConflictException(
                        "The selected resource is not available or is inactive.");
                if (resource.ResourceType != package.ResourceType)
                    throw new ConflictException(
                        "The selected resource is not compatible with the chosen service package.");

                // Check for overlapping bookings for this resource
                var hasOverlap = await _dbContext.Bookings
                    .AnyAsync(b =>
                        b.ResourceId == resource.Id &&
                        b.BookingStatus != BookingStatus.Cancelled &&
                        b.ScheduledStart < scheduledEnd &&
                        b.ScheduledEnd > scheduledStart,
                        cancellationToken);

                if (hasOverlap)
                    throw new ConflictException("The selected resource is already booked for the specified time range.");

                // Generate reference via booking_ref_seq
                var nextVal = await _dbContext.Database
                    .SqlQuery<long>($"SELECT nextval('booking_ref_seq') AS \"Value\"")
                    .FirstAsync(cancellationToken);
                var referenceNumber = $"BKG-{nextVal:D4}";

                guest = new BookingGuest
                {
                    Id = Guid.NewGuid(),
                    FirstName = req.FirstName.Trim(),
                    LastName = req.LastName.Trim(),
                    Email = req.Email.Trim(),
                    Phone = $"{req.PhoneCountryCode}{req.PhoneNumber}",
                    IsStudentVerified = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                booking = new Booking
                {
                    Id = Guid.NewGuid(),
                    ReferenceNumber = referenceNumber,
                    BookingType = BookingType.Online,
                    BookingStatus = BookingStatus.Pending,
                    PaymentStatus = PaymentStatus.Unpaid,
                    ResourceId = resource.Id,
                    ServicePackageId = package.Id,
                    ScheduledStart = scheduledStart,
                    ScheduledEnd = scheduledEnd,
                    IsStudent = req.IsStudent,
                    PaxCount = 1,
                    SnapshotPackageName = package.Name,
                    SnapshotPricePerUnit = package.PriceAmount,
                    SnapshotHourlyRate = snapshotHourlyRate,
                    SnapshotPaymentScheme = PaymentScheme.Upfront,
                    BaseAmount = baseAmount,
                    DiscountAmount = discountAmount,
                    DownpaymentAmount = 0m,
                    BalanceAmount = totalAmount,
                    TotalAmount = totalAmount,
                    Guest = guest,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };

                _dbContext.Bookings.Add(booking);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            // 6. Upload screenshots and create payment records (outside transaction)
            var paymentAmount = totalAmount / screenshotFiles.Count;
            foreach (var file in screenshotFiles)
            {
                var key = FileSecurityHelper.BuildStorageKey(
                    $"payment-proofs/{booking.Id}", file.FileName);
                var url = await _storageService.UploadFileAsync(
                    key, new MemoryStream(file.Bytes), file.ContentType);

                _dbContext.BookingPayments.Add(new BookingPayment
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    Amount = Math.Round(paymentAmount, 2),
                    PaymentMethod = PaymentMethod.ImageAttachment,
                    PaymentType = PaymentType.Downpayment,
                    PaymentValidationStatus = PaymentValidationStatus.PendingValidation,
                    ProofOfPaymentUrl = url,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            await _dbContext.SaveChangesAsync(cancellationToken);

            // 7. Send emails with the screenshots attached directly
            var capturedBooking = booking;
            var capturedGuest = guest;
            _ = Task.Run(async () =>
            {
                try
                {
                    var attachments = screenshotFiles
                        .Select(f => new EmailAttachmentDto
                        {
                            FileName = f.FileName,
                            ContentType = f.ContentType,
                            Content = f.Bytes,
                        })
                        .ToList();

                    await _emailService.SendEmailAsync(
                        _settings.AdminNotificationEmails,
                        $"[Coworking Booking] {capturedBooking.ReferenceNumber} — {capturedGuest.FirstName} {capturedGuest.LastName}",
                        BuildAdminEmail(capturedBooking, capturedGuest, _settings.ClientBaseUrl),
                        attachments);

                    await _emailService.SendEmailAsync(
                        capturedGuest.Email!,
                        $"Booking Received — {capturedBooking.ReferenceNumber}",
                        BuildCustomerEmail(capturedBooking, capturedGuest));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to send emails for coworking booking {ReferenceNumber}",
                        capturedBooking.ReferenceNumber);
                }
            }, CancellationToken.None);

            return new SubmitCoworkingBookingResponse
            {
                BookingId = booking.Id,
                ReferenceNumber = booking.ReferenceNumber,
                GuestName = $"{guest.FirstName} {guest.LastName}",
                PackageName = booking.SnapshotPackageName,
                ScheduledStart = booking.ScheduledStart,
                ScheduledEnd = booking.ScheduledEnd,
                TotalAmount = booking.TotalAmount,
                Message = "Your booking has been received. We will confirm it once payment has been verified.",
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static DateTime ResolveScheduledEnd(
            Domain.Entity.Coworking.ServicePackage package,
            TimeOnly startTime, TimeOnly? endTime, DateOnly date)
        {
            if (package.PackageType == PackageType.Hourly)
            {
                if (!endTime.HasValue)
                    throw new ConflictException("End time is required for hourly packages.");
                return date.ToDateTime(endTime.Value, DateTimeKind.Utc);
            }
            if (package.DurationHours.HasValue)
                return date.ToDateTime(startTime, DateTimeKind.Utc).AddHours(package.DurationHours.Value);
            if (package.DurationDays.HasValue)
                return date.ToDateTime(startTime, DateTimeKind.Utc).AddDays(package.DurationDays.Value);
            return endTime.HasValue
                ? date.ToDateTime(endTime.Value, DateTimeKind.Utc)
                : date.ToDateTime(startTime, DateTimeKind.Utc).AddHours(1);
        }

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

        private static string BuildAdminEmail(Booking booking, BookingGuest guest, string clientBaseUrl)
        {
            var normalizedClientBaseUrl = (clientBaseUrl ?? string.Empty).TrimEnd('/');
            var bookingId = WebUtility.UrlEncode(booking.Id.ToString());
            var guestName = WebUtility.HtmlEncode($"{guest.FirstName} {guest.LastName}");
            var email = WebUtility.HtmlEncode(guest.Email ?? "N/A");
            var phone = WebUtility.HtmlEncode(guest.Phone);
            var package = WebUtility.HtmlEncode(booking.SnapshotPackageName);
            var resource = WebUtility.HtmlEncode(booking.Resource?.Name ?? "N/A");
            var start = booking.ScheduledStart.ToString("yyyy-MM-dd HH:mm") + " UTC";
            var end = booking.ScheduledEnd.ToString("yyyy-MM-dd HH:mm") + " UTC";
            var amount = booking.TotalAmount.ToString("N2");
            var isStudent = booking.IsStudent ? "Yes" : "No";

            return $"""
                <h2>New Online Coworking Booking</h2>
                <p>A new booking was submitted through the public booking form.
                   Payment screenshots are attached to this email.</p>
                <h3>Booking Details</h3>
                <ul>
                    <li><strong>Reference:</strong> {booking.ReferenceNumber}</li>
                    <li><strong>Package:</strong> {package}</li>
                    <li><strong>Resource:</strong> {resource}</li>
                    <li><strong>Start (UTC):</strong> {start}</li>
                    <li><strong>End (UTC):</strong> {end}</li>
                    <li><strong>Total Amount:</strong> &#8369;{amount}</li>
                </ul>
                <h3>Guest Information</h3>
                <ul>
                    <li><strong>Name:</strong> {guestName}</li>
                    <li><strong>Email:</strong> {email}</li>
                    <li><strong>Phone:</strong> {phone}</li>
                    <li><strong>Is Student:</strong> {isStudent}</li>
                </ul>
                <p>Please log in to the admin dashboard to validate the payment and confirm this booking.</p>
                <p>Track your bookings here: <a href="{normalizedClientBaseUrl}/my-bookings/{bookingId}">{normalizedClientBaseUrl}/my-bookings/{bookingId}</a></p>
                """;

        }

        private static string BuildCustomerEmail(Booking booking, BookingGuest guest)
        {
            var firstName = WebUtility.HtmlEncode(guest.FirstName);
            var package = WebUtility.HtmlEncode(booking.SnapshotPackageName);
            var resource = WebUtility.HtmlEncode(booking.Resource?.Name ?? "N/A");
            var start = booking.ScheduledStart.ToString("yyyy-MM-dd HH:mm") + " UTC";
            var end = booking.ScheduledEnd.ToString("yyyy-MM-dd HH:mm") + " UTC";
            var amount = booking.TotalAmount.ToString("N2");

            return $"""
                <h2>We received your booking!</h2>
                <p>Hi {firstName},</p>
                <p>Thank you for booking with us. Your booking has been received and is pending payment verification.</p>
                <h3>Booking Summary</h3>
                <ul>
                    <li><strong>Reference Number:</strong> {booking.ReferenceNumber}</li>
                    <li><strong>Package:</strong> {package}</li>
                    <li><strong>Resource:</strong> {resource}</li>
                    <li><strong>Start (UTC):</strong> {start}</li>
                    <li><strong>End (UTC):</strong> {end}</li>
                    <li><strong>Total Amount:</strong> &#8369;{amount}</li>
                </ul>
                <p>We will review your payment and send you a confirmation once it has been verified.
                   This typically takes within a few hours during business hours.</p>
                <p>If you have any questions, feel free to contact us.</p>
                """;
        }
    }
}

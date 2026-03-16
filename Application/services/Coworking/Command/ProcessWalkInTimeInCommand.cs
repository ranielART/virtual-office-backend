using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using Domain.Entity.Coworking;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class ProcessWalkInTimeInCommand : IRequest<WalkInTimeInResponse>
    {
        public WalkInTimeInRequest Request { get; }
        public string FrontDeskUserId { get; }

        public ProcessWalkInTimeInCommand(WalkInTimeInRequest request, string frontDeskUserId)
        {
            Request = request;
            FrontDeskUserId = frontDeskUserId;
        }
    }

    public class ProcessWalkInTimeInCommandHandler
        : IRequestHandler<ProcessWalkInTimeInCommand, WalkInTimeInResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public ProcessWalkInTimeInCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<WalkInTimeInResponse> Handle(
            ProcessWalkInTimeInCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            // 1. Validate resource
            var resource = await _dbContext.CoworkingResources
                .FirstOrDefaultAsync(r => r.Id == req.ResourceId && r.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Resource '{req.ResourceId}' not found or is inactive.");

            // 2. Validate package
            var package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == req.ServicePackageId && p.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Service package '{req.ServicePackageId}' not found or is inactive.");

            // 3. EstimatedHours required for Hourly packages
            if (package.PackageType == PackageType.Hourly && !req.EstimatedHours.HasValue)
                throw new ConflictException("Estimated hours is required for hourly packages.");

            // 4. Validate chosen payment scheme against what the package allows
            var schemeAllowed = req.PaymentScheme switch
            {
                PaymentScheme.Upfront  => package.AllowsUpfront,
                PaymentScheme.Postpaid => package.AllowsPostpaid,
                PaymentScheme.Daily    => package.AllowsDaily,
                _                     => false
            };

            if (!schemeAllowed)
                throw new ConflictException(
                    $"Payment scheme '{req.PaymentScheme}' is not allowed for package '{package.Name}'. " +
                    $"Allowed: " +
                    $"{(package.AllowsUpfront ? "Upfront " : "")}" +
                    $"{(package.AllowsPostpaid ? "Postpaid " : "")}" +
                    $"{(package.AllowsDaily ? "Daily" : "")}".Trim());

            // 5. Begin transaction
            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var now = DateTime.UtcNow;

                // 6. Resolve ScheduledEnd based on package type
                var requestedStart = now;
                var requestedEnd   = ResolveScheduledEnd(package, req.EstimatedHours, now);

                // 6. Re-check availability inside transaction
                var isUnavailable = await _dbContext.Bookings.AnyAsync(b =>
                    b.ResourceId == req.ResourceId &&
                    b.BookingStatus != BookingStatus.Cancelled &&
                    b.BookingStatus != BookingStatus.NoShow &&
                    b.BookingStatus != BookingStatus.CheckedOut &&
                    b.BookingStatus != BookingStatus.Overstayed &&
                    b.ScheduledStart < requestedEnd &&
                    b.ScheduledEnd   > requestedStart,
                    cancellationToken);

                if (isUnavailable)
                    throw new ConflictException("This resource is currently occupied.");

                // 7. Generate reference number using PostgreSQL sequence
                var nextVal = await _dbContext.Database
                    .SqlQuery<long>($"SELECT nextval('booking_ref_seq') AS \"Value\"")
                    .FirstAsync(cancellationToken);
                var referenceNumber = $"BKG-{nextVal:D4}";

                // 8. Compute SnapshotHourlyRate
                decimal snapshotHourlyRate;
                if (package.PackageType == PackageType.Hourly)
                {
                    snapshotHourlyRate = package.PriceAmount;
                }
                else if (package.DurationHours.HasValue)
                {
                    snapshotHourlyRate = package.PriceAmount / package.DurationHours.Value;
                }
                else if (package.DurationDays.HasValue)
                {
                    snapshotHourlyRate = package.PriceAmount / (package.DurationDays.Value * 8m);
                }
                else
                {
                    var hourlyPackage = await _dbContext.ServicePackages
                        .FirstOrDefaultAsync(p =>
                            p.ResourceType == resource.ResourceType &&
                            p.PackageType   == PackageType.Hourly &&
                            p.IsActive,
                            cancellationToken);
                    snapshotHourlyRate = hourlyPackage?.PriceAmount ?? 0m;
                }

                // 9. Compute pricing
                var baseAmount = package.PackageType == PackageType.Hourly
                    ? req.EstimatedHours!.Value * package.PriceAmount
                    : package.PriceAmount;

                var discountAmount = req.IsStudent && package.IsStudentEligible
                    ? baseAmount * (package.StudentDiscountPercent / 100m)
                    : 0m;

                var totalAmount   = baseAmount - discountAmount;
                var balanceAmount = totalAmount;

                // 10. Compute daily rate (only relevant for Daily scheme)
                decimal? dailyRate = req.PaymentScheme == PaymentScheme.Daily && package.DurationDays.HasValue
                    ? Math.Round(totalAmount / package.DurationDays.Value, 2)
                    : null;

                // 11. Create BookingGuest
                var guest = new BookingGuest
                {
                    Id                = Guid.NewGuid(),
                    FirstName         = req.GuestFirstName,
                    LastName          = req.GuestLastName,
                    Phone             = req.GuestPhone,
                    Email             = req.GuestEmail,
                    IsStudentVerified = req.IsStudent,
                    CreatedAt         = now,
                    UpdatedAt         = now
                };

                // 12. Create Booking
                var booking = new Booking
                {
                    Id                      = Guid.NewGuid(),
                    ReferenceNumber         = referenceNumber,
                    BookingType             = BookingType.WalkIn,
                    BookingStatus           = BookingStatus.CheckedIn,
                    PaymentStatus           = PaymentStatus.Unpaid,
                    ResourceId              = req.ResourceId,
                    ServicePackageId        = req.ServicePackageId,
                    FrontDeskUserId         = command.FrontDeskUserId,
                    ScheduledStart          = requestedStart,
                    ScheduledEnd            = requestedEnd,
                    ActualStart             = requestedStart,
                    IsStudent               = req.IsStudent,
                    PaxCount                = 1,
                    DownpaymentAmount       = 0m,
                    SnapshotPackageName     = package.Name,
                    SnapshotPricePerUnit    = package.PriceAmount,
                    SnapshotHourlyRate      = snapshotHourlyRate,
                    SnapshotPaymentScheme   = req.PaymentScheme,
                    BaseAmount              = baseAmount,
                    DiscountAmount          = discountAmount,
                    TotalAmount             = totalAmount,
                    BalanceAmount           = balanceAmount,
                    Guest                   = guest,
                    CreatedAt               = now,
                    UpdatedAt               = now
                };

                // 13. Persist and commit
                _dbContext.Bookings.Add(booking);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new WalkInTimeInResponse
                {
                    BookingId           = booking.Id,
                    ReferenceNumber     = booking.ReferenceNumber,
                    GuestName           = $"{guest.FirstName} {guest.LastName}",
                    ResourceName        = resource.Name,
                    ScheduledStart      = booking.ScheduledStart,
                    ScheduledEnd        = booking.ScheduledEnd,
                    SnapshotPackageName = booking.SnapshotPackageName,
                    PaymentScheme       = booking.SnapshotPaymentScheme,
                    IsStudent           = booking.IsStudent,
                    BaseAmount          = booking.BaseAmount,
                    DiscountAmount      = booking.DiscountAmount,
                    TotalAmount         = booking.TotalAmount,
                    DailyRate           = dailyRate
                };
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// Resolves the booking's scheduled end time based on the package type.
        ///
        /// Hourly    → now + estimatedHours (open-ended metered billing)
        /// Fixed hr  → now + DurationHours  (half-day, full-day, day-pass)
        /// Fixed day → now + DurationDays calendar days (weekly, monthly)
        /// </summary>
        private static DateTime ResolveScheduledEnd(
            Domain.Entity.Coworking.ServicePackage package,
            decimal? estimatedHours,
            DateTime now)
        {
            if (package.PackageType == PackageType.Hourly)
                return now.AddHours((double)estimatedHours!.Value);

            if (package.DurationHours.HasValue)
                return now.AddHours(package.DurationHours.Value);

            if (package.DurationDays.HasValue)
                return now.AddDays(package.DurationDays.Value);

            // Fallback — should not reach here for well-configured packages
            return now.AddHours((double)(estimatedHours ?? 1));
        }
    }
}

using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class CheckAvailabilityQuery : IRequest<List<AvailabilityResponse>>
    {
        public DateOnly Date { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public ResourceType? ResourceType { get; set; }
    }

    public class CheckAvailabilityQueryHandler
        : IRequestHandler<CheckAvailabilityQuery, List<AvailabilityResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public CheckAvailabilityQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AvailabilityResponse>> Handle(
            CheckAvailabilityQuery request, CancellationToken cancellationToken)
        {
            var requestedStart = request.Date.ToDateTime(request.StartTime).ToUniversalTime();
            var requestedEnd   = request.Date.ToDateTime(request.EndTime).ToUniversalTime();

            var resourceQuery = _dbContext.CoworkingResources
                .Where(r => r.IsActive);

            if (request.ResourceType.HasValue)
                resourceQuery = resourceQuery.Where(r => r.ResourceType == request.ResourceType.Value);

            var resources = await resourceQuery
                .OrderBy(r => r.ResourceType)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken);

            var result = new List<AvailabilityResponse>();

            foreach (var resource in resources)
            {
                var isUnavailable = await _dbContext.Bookings.AnyAsync(b =>
                    b.ResourceId == resource.Id &&
                    b.BookingStatus != Domain.Enum.BookingStatus.Cancelled &&
                    b.BookingStatus != Domain.Enum.BookingStatus.NoShow &&
                    b.BookingStatus != Domain.Enum.BookingStatus.CheckedOut &&
                    b.BookingStatus != Domain.Enum.BookingStatus.Overstayed &&
                    b.ScheduledStart < requestedEnd &&
                    b.ScheduledEnd   > requestedStart,
                    cancellationToken);

                result.Add(new AvailabilityResponse
                {
                    ResourceId   = resource.Id,
                    ResourceName = resource.Name,
                    ResourceType = resource.ResourceType,
                    Capacity     = resource.Capacity,
                    IsAvailable  = !isUnavailable
                });
            }

            return result;
        }
    }
}

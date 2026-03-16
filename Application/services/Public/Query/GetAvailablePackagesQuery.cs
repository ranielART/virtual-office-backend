using Application.Services.Public.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Public.Query
{
    public class GetAvailablePackagesQuery : IRequest<List<AvailablePackageResponse>>
    {
        public ResourceType? ResourceType { get; set; }

        public GetAvailablePackagesQuery(ResourceType? resourceType = null)
        {
            ResourceType = resourceType;
        }
    }

    public class GetAvailablePackagesQueryHandler
        : IRequestHandler<GetAvailablePackagesQuery, List<AvailablePackageResponse>>
    {
        private readonly Persistence.Data.ApplicationDbContext _dbContext;

        public GetAvailablePackagesQueryHandler(Persistence.Data.ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AvailablePackageResponse>> Handle(
            GetAvailablePackagesQuery request, CancellationToken cancellationToken)
        {
            var query = _dbContext.ServicePackages.AsQueryable();

            if (request.ResourceType.HasValue)
                query = query.Where(p => p.ResourceType == request.ResourceType.Value);

            var packages = await query
                .Where(p => p.IsActive)
                .OrderBy(p => p.ResourceType)
                .ThenBy(p => p.PackageType)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);

            return packages.Select(p => new AvailablePackageResponse
            {
                Id = p.Id,
                Name = p.Name,
                ResourceType = p.ResourceType,
                PackageType = p.PackageType,
                PriceAmount = p.PriceAmount,
                DurationHours = p.DurationHours,
                DurationDays = p.DurationDays,
                MaxCapacity = p.MaxCapacity,
                IsStudentEligible = p.IsStudentEligible,
                StudentDiscountPercent = p.StudentDiscountPercent,
                DownpaymentPercent = p.DownpaymentPercent,
                AllowsUpfront  = p.AllowsUpfront,
                AllowsPostpaid = p.AllowsPostpaid,
                AllowsDaily    = p.AllowsDaily,
                CreatedAt = p.CreatedAt
            }).ToList();
        }
    }
}

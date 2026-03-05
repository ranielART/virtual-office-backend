using Application.Services.Coworking.Command;
using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetAllServicePackagesQuery : IRequest<List<ServicePackageResponse>>
    {
        public ResourceType? ResourceType { get; }
        public bool? IsActive { get; }

        public GetAllServicePackagesQuery(ResourceType? resourceType = null, bool? isActive = null)
        {
            ResourceType = resourceType;
            IsActive = isActive;
        }
    }

    public class GetAllServicePackagesQueryHandler
        : IRequestHandler<GetAllServicePackagesQuery, List<ServicePackageResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetAllServicePackagesQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<ServicePackageResponse>> Handle(
            GetAllServicePackagesQuery request, CancellationToken cancellationToken)
        {
            var query = _dbContext.ServicePackages.AsQueryable();

            if (request.ResourceType.HasValue)
                query = query.Where(p => p.ResourceType == request.ResourceType.Value);

            if (request.IsActive.HasValue)
                query = query.Where(p => p.IsActive == request.IsActive.Value);

            var packages = await query
                .OrderBy(p => p.ResourceType)
                .ThenBy(p => p.PackageType)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);

            return packages.Select(CreateServicePackageCommandHandler.MapToResponse).ToList();
        }
    }
}

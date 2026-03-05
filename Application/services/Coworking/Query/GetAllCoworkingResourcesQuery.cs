using Application.Services.Coworking.Response;
using Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetAllCoworkingResourcesQuery : IRequest<List<CoworkingResourceResponse>>
    {
        public ResourceType? ResourceType { get; }
        public bool? IsActive { get; }

        public GetAllCoworkingResourcesQuery(ResourceType? resourceType = null, bool? isActive = null)
        {
            ResourceType = resourceType;
            IsActive = isActive;
        }
    }

    public class GetAllCoworkingResourcesQueryHandler
        : IRequestHandler<GetAllCoworkingResourcesQuery, List<CoworkingResourceResponse>>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetAllCoworkingResourcesQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<CoworkingResourceResponse>> Handle(
            GetAllCoworkingResourcesQuery request, CancellationToken cancellationToken)
        {
            var query = _dbContext.CoworkingResources.AsQueryable();

            if (request.ResourceType.HasValue)
                query = query.Where(r => r.ResourceType == request.ResourceType.Value);

            if (request.IsActive.HasValue)
                query = query.Where(r => r.IsActive == request.IsActive.Value);

            var resources = await query
                .OrderBy(r => r.SortOrder)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken);

            return resources.Select(r => new CoworkingResourceResponse
            {
                Id = r.Id,
                Name = r.Name,
                ResourceType = r.ResourceType,
                Capacity = r.Capacity,
                Description = r.Description,
                IsActive = r.IsActive,
                SortOrder = r.SortOrder,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt
            }).ToList();
        }
    }
}

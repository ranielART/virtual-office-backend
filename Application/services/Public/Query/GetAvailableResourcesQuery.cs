using Application.Services.Public.Response;
using Domain.Entity.Coworking;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Public.Query
{
    public class GetAvailableResourcesQuery : IRequest<List<AvailableResourceResponse>>
    {
    }

    public class GetAvailableResourcesQueryHandler
        : IRequestHandler<GetAvailableResourcesQuery, List<AvailableResourceResponse>>
    {
        private readonly Persistence.Data.ApplicationDbContext _dbContext;

        public GetAvailableResourcesQueryHandler(Persistence.Data.ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<AvailableResourceResponse>> Handle(
            GetAvailableResourcesQuery request, CancellationToken cancellationToken)
        {
            var resources = await _dbContext.CoworkingResources
                .Where(r => r.IsActive)
                .OrderBy(r => r.Name)
                .ToListAsync(cancellationToken);

            return resources.Select(r => new AvailableResourceResponse
            {
                Id = r.Id,
                Name = r.Name,
                ResourceType = r.ResourceType,
                Capacity = r.Capacity,
                Description = r.Description,
                CreatedAt = r.CreatedAt
            }).ToList();
        }
    }
}

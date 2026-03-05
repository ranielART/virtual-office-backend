using Application.Common.Exceptions;
using Application.Services.Coworking.Response;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Query
{
    public class GetCoworkingResourceByIdQuery : IRequest<CoworkingResourceResponse>
    {
        public Guid Id { get; }
        public GetCoworkingResourceByIdQuery(Guid id) => Id = id;
    }

    public class GetCoworkingResourceByIdQueryHandler
        : IRequestHandler<GetCoworkingResourceByIdQuery, CoworkingResourceResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public GetCoworkingResourceByIdQueryHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CoworkingResourceResponse> Handle(
            GetCoworkingResourceByIdQuery request, CancellationToken cancellationToken)
        {
            var resource = await _dbContext.CoworkingResources
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
                ?? throw new NotFoundException($"Resource with ID {request.Id} was not found.");

            return new CoworkingResourceResponse
            {
                Id = resource.Id,
                Name = resource.Name,
                ResourceType = resource.ResourceType,
                Capacity = resource.Capacity,
                Description = resource.Description,
                IsActive = resource.IsActive,
                SortOrder = resource.SortOrder,
                CreatedAt = resource.CreatedAt,
                UpdatedAt = resource.UpdatedAt
            };
        }
    }
}

using Application.Common.Exceptions;
using Application.Services.Coworking.Request;
using Application.Services.Coworking.Response;
using Domain.Entity.Coworking;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.Coworking.Command
{
    public class CreateCoworkingResourceCommand : IRequest<CoworkingResourceResponse>
    {
        public CreateCoworkingResourceRequest Request { get; }
        public CreateCoworkingResourceCommand(CreateCoworkingResourceRequest request) => Request = request;
    }

    public class CreateCoworkingResourceCommandHandler
        : IRequestHandler<CreateCoworkingResourceCommand, CoworkingResourceResponse>
    {
        private readonly ApplicationDbContext _dbContext;

        public CreateCoworkingResourceCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<CoworkingResourceResponse> Handle(
            CreateCoworkingResourceCommand command, CancellationToken cancellationToken)
        {
            var req = command.Request;

            var nameExists = await _dbContext.CoworkingResources
                .AnyAsync(r => r.Name.ToLower() == req.Name.ToLower(), cancellationToken);

            if (nameExists)
                throw new ConflictException($"A resource named '{req.Name}' already exists.");

            var resource = new CoworkingResource
            {
                Id = Guid.NewGuid(),
                Name = req.Name,
                ResourceType = req.ResourceType,
                Capacity = req.Capacity,
                Description = req.Description,
                IsActive = true,
                SortOrder = req.SortOrder,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.CoworkingResources.Add(resource);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return MapToResponse(resource);
        }

        private static CoworkingResourceResponse MapToResponse(CoworkingResource r) => new()
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
        };
    }
}

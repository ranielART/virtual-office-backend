using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.User.Command
{
    public class LogoutCommand : IRequest<Unit>
    {
        public string RefreshToken { get; set; }

        public LogoutCommand(string refreshToken)
        {
            RefreshToken = refreshToken;
        }
    }

    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
    {
        private readonly ApplicationDbContext _dbContext;

        public LogoutCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var refreshToken = await _dbContext.Set<Domain.Entity.Identity.RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

            if (refreshToken != null)
            {
                refreshToken.Invalidated = true;
                refreshToken.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return Unit.Value;
        }
    }
}

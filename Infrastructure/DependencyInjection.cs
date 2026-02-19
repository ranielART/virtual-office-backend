using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Application.Common.Interfaces;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // Register Token Service
            services.AddScoped<ITokenService, TokenService>();

            // Register Email Service
            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}

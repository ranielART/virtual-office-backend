using Application.Common.Behaviors;
using Application.Services.User;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {

            services.AddHttpContextAccessor();
            // Add MediatR
            services.AddMediatR(cfg =>
            {

                cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

            });

            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            // 3. Register Pipeline Behavior (Connects MediatR to FluentValidation)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            // Add AutoMapper
            services.AddAutoMapper(typeof(UserMappingProfile));

            return services;
        }
    }
}

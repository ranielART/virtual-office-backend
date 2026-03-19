
using Application.Common.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using WebApi.Infrastructure;

namespace DependencyInjection
{
    public static class DependencyInjection
    {

        public static IServiceCollection AddWebApi(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower)));
            services.AddEndpointsApiExplorer();


            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "virtual-office-booking-api",
                    ValidAudience = configuration["Jwt:Audience"] ?? "virtual-office-booking-client",
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret Key is not configured"))),

                    // This is the key fix - tells JWT handler which claim to use for roles
                    RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                    NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
                };
            });

            // Add Authorization
            services.AddAuthorization();



            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Virtual Office API", Version = "v1" });

                // Define the Security Scheme
                options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                    Description = "Enter 'Bearer' [space] and then your token. Example: 'Bearer abcdef12345'"
                });

                // Apply the requirement globally
                options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
                {
                    {
                        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                        {
                            Reference = new Microsoft.OpenApi.Models.OpenApiReference
                            {
                                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // Rate limiting — applied to public booking and email-verification endpoints
            services.AddRateLimiter(options =>
            {
                // "booking": max 10 booking submissions per IP per minute
                options.AddFixedWindowLimiter("booking", limiter =>
                {
                    limiter.PermitLimit = 10;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.QueueLimit = 0;
                });

                // "email-verification": max 3 OTP requests per IP per 5 minutes
                options.AddFixedWindowLimiter("email-verification", limiter =>
                {
                    limiter.PermitLimit = 3;
                    limiter.Window = TimeSpan.FromMinutes(5);
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.QueueLimit = 0;
                });

                // "contact-us": max 5 contact form submissions per IP per hour
                options.AddFixedWindowLimiter("contact-us", limiter =>
                {
                    limiter.PermitLimit = 5;
                    limiter.Window = TimeSpan.FromHours(1);
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    limiter.QueueLimit = 0;
                });

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            });

            // Coworking module settings
            services.Configure<CoworkingSettings>(configuration.GetSection("CoworkingSettings"));

            services.AddCors(options =>
            {
                options.AddPolicy("Cors", policy =>
                {
                    policy.WithOrigins("http://localhost:5133", "https://localhost:7129")
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            });



            return services;
        }

        public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
        {
            // 1. Clear default logging providers (keeps logs clean)
            builder.Logging.ClearProviders();

            // 2. Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .WriteTo.Console(theme: AnsiConsoleTheme.Code, outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
                .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // 3. Register Serilog with the Host
            builder.Host.UseSerilog();

            return builder;
        }
    }
}
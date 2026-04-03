# .NET 9 Clean Architecture Boilerplate — Auth, Users & Role-Based CRUD

> **Purpose:** Step-by-step instructions for building a production-ready .NET 9 Web API with Clean Architecture, CQRS (MediatR), JWT auth with rotating refresh tokens, role-based user CRUD, global error handling, and FluentValidation. Each phase is self-contained and builds on the previous one. Every file path, class, and configuration is explicit so any AI or developer can reproduce it.
>
> **Reference project:** `virtual-office-booking-system-backend` — a coworking space booking system with Admin/Frontdesk/Customer roles.

---

## Table of Contents

1. [Phase 1 — Solution & Clean Architecture Scaffold](#phase-1--solution--clean-architecture-scaffold)
2. [Phase 2 — Domain Layer](#phase-2--domain-layer)
3. [Phase 3 — Persistence Layer](#phase-3--persistence-layer)
4. [Phase 4 — Application Layer (CQRS, Validation, Pipeline)](#phase-4--application-layer-cqrs-validation-pipeline)
5. [Phase 5 — Infrastructure Layer (Tokens, Storage, Email)](#phase-5--infrastructure-layer-tokens-storage-email)
6. [Phase 6 — WebApi Layer (DI, Middleware, Program.cs)](#phase-6--webapi-layer-di-middleware-programcs)
7. [Phase 7 — Auth System (Login, Refresh, Logout, Change/Reset Password)](#phase-7--auth-system-login-refresh-logout-changereset-password)
8. [Phase 8 — Role-Based User CRUD (Admin + Generic Role Pattern)](#phase-8--role-based-user-crud-admin--generic-role-pattern)
9. [Phase 9 — Verification & Testing](#phase-9--verification--testing)

---

## Phase 1 — Solution & Clean Architecture Scaffold

### 1.1 Create the solution and projects

```bash
# Create solution
dotnet new sln -n MyProject

# Create class libraries for each layer
dotnet new classlib -n Domain -f net9.0
dotnet new classlib -n Persistence -f net9.0
dotnet new classlib -n Application -f net9.0
dotnet new classlib -n Infrastructure -f net9.0

# Create the Web API host
dotnet new webapi -n WebApi -f net9.0 --no-openapi

# Add all projects to the solution
dotnet sln add Domain/Domain.csproj
dotnet sln add Persistence/Persistence.csproj
dotnet sln add Application/Application.csproj
dotnet sln add Infrastructure/Infrastructure.csproj
dotnet sln add WebApi/WebApi.csproj
```

### 1.2 Set project references (dependency flow)

```
Domain           → (no dependencies)
Persistence      → Domain
Application      → Domain, Persistence
Infrastructure   → Application
WebApi           → Application, Infrastructure, Persistence
```

```bash
# Persistence depends on Domain
dotnet add Persistence/Persistence.csproj reference Domain/Domain.csproj

# Application depends on Domain and Persistence
dotnet add Application/Application.csproj reference Domain/Domain.csproj
dotnet add Application/Application.csproj reference Persistence/Persistence.csproj

# Infrastructure depends on Application
dotnet add Infrastructure/Infrastructure.csproj reference Application/Application.csproj

# WebApi depends on Application, Infrastructure, Persistence
dotnet add WebApi/WebApi.csproj reference Application/Application.csproj
dotnet add WebApi/WebApi.csproj reference Infrastructure/Infrastructure.csproj
dotnet add WebApi/WebApi.csproj reference Persistence/Persistence.csproj
```

### 1.3 Install NuGet packages

**Domain:**
```bash
dotnet add Domain package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 9.0.13
dotnet add Domain package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.13
```

**Persistence:**
```bash
dotnet add Persistence package Npgsql.EntityFrameworkCore.PostgreSQL --version 9.0.4
dotnet add Persistence package EFCore.NamingConventions --version 9.0.0
dotnet add Persistence package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 9.0.13
dotnet add Persistence package Microsoft.Extensions.Configuration.Abstractions --version 9.0.13
dotnet add Persistence package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.13
dotnet add Persistence package Newtonsoft.Json --version 13.0.4
```

**Application:**
```bash
dotnet add Application package MediatR --version 12.4.1
dotnet add Application package FluentValidation --version 12.1.1
dotnet add Application package FluentValidation.DependencyInjectionExtensions --version 12.1.1
dotnet add Application package AutoMapper --version 12.0.1
dotnet add Application package AutoMapper.Extensions.Microsoft.DependencyInjection --version 12.0.1
dotnet add Application package Serilog --version 4.3.1
dotnet add Application package Serilog.Sinks.Console --version 6.0.0
dotnet add Application package System.IdentityModel.Tokens.Jwt --version 8.0.2
dotnet add Application package Microsoft.AspNetCore.Http --version 2.3.9
dotnet add Application package Microsoft.Extensions.DependencyInjection.Abstractions --version 9.0.13
```

**Infrastructure:**
```bash
dotnet add Infrastructure package Azure.Storage.Blobs --version 12.19.0
dotnet add Infrastructure package Azure.Communication.Email --version 1.1.0
```

**WebApi:**
```bash
dotnet add WebApi package DotNetEnv --version 3.1.1
dotnet add WebApi package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.0.13
dotnet add WebApi package Microsoft.EntityFrameworkCore.Design --version 9.0.12
dotnet add WebApi package Microsoft.EntityFrameworkCore.Tools --version 9.0.12
dotnet add WebApi package Serilog.AspNetCore --version 9.0.0
dotnet add WebApi package Serilog.Enrichers.Environment --version 3.0.1
dotnet add WebApi package Serilog.Enrichers.Thread --version 4.0.0
dotnet add WebApi package Serilog.Settings.Configuration --version 9.0.0
dotnet add WebApi package Serilog.Sinks.File --version 6.0.0
dotnet add WebApi package Swashbuckle.AspNetCore --version 7.0.0
```

### 1.4 Create the directory structure

```
Domain/
  Common/
  Entity/
    Identity/
  Enum/

Persistence/
  Data/
  Configurations/
  Seeders/
  Migrations/        ← auto-generated by EF

Application/
  Common/
    Behaviors/
    Exceptions/
    Interfaces/
    Models/
  Services/
    User/
      Command/
      Query/
        User/
      Request/
      Response/
      Validator/

Infrastructure/
  Configuration/
  Services/

WebApi/
  Controllers/
  Infrastructure/
  Docs/
  Properties/
```

Delete the default `Class1.cs` from each class library and `WeatherForecast*` from WebApi.

### 1.5 Environment file

Create a `.env` file in the solution root (add to `.gitignore`):

```env
DB_HOST=localhost
DB_PORT=5432
DB_NAME=my_project_db
DB_USER=postgres
DB_PASSWORD=CHANGE_ME

JWT_SECRET=a-very-long-random-string-at-least-32-characters-long-for-hmac-sha256
JWT_EXPIRY_MINUTES=15
JWT_REFRESH_TOKEN_EXPIRY_DAYS=7
JWT_ISSUER=my-project-api
JWT_AUDIENCE=my-project-client

AZURE_STORAGE_CONNECTION_STRING=
AZURE_STORAGE_CONTAINER_NAME=
AZURE_COMMUNICATION_CONNECTION_STRING=
AZURE_EMAIL_SENDER=
```

---

## Phase 2 — Domain Layer

This layer has zero dependencies on any other project layer. It contains entities, enums, and base classes.

### 2.1 Base entity

**`Domain/Common/GuidBaseEntity.cs`**

```csharp
namespace Domain.Common
{
    public class GuidBaseEntity
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public GuidBaseEntity()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

### 2.2 Enums

**`Domain/Enum/RegistrationStatus.cs`**

```csharp
namespace Domain.Enum
{
    public enum RegistrationStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }
}
```

Add any domain-specific enums here later. Each enum value should start at 1 (never 0) so that unset values are distinguishable.

### 2.3 Identity entities

**`Domain/Entity/Identity/User.cs`**

```csharp
using Domain.Enum;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entity.Identity
{
    public class User : IdentityUser
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string? Address { get; set; }
        public RegistrationStatus Status { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public bool IsActive { get; set; } = true;

        public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
```

**`Domain/Entity/Identity/Role.cs`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace Domain.Entity.Identity
{
    public class Role : IdentityRole<string>
    {
        public virtual ICollection<UserRole> Users { get; set; } = new List<UserRole>();
    }
}
```

**`Domain/Entity/Identity/UserRole.cs`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace Domain.Entity.Identity
{
    public class UserRole : IdentityUserRole<string>
    {
        public virtual User User { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;
    }
}
```

**`Domain/Entity/Identity/RefreshToken.cs`**

```csharp
namespace Domain.Entity.Identity
{
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public string Token { get; set; } = string.Empty;
        public string JwtId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool Used { get; set; }
        public bool Invalidated { get; set; }

        public string UserId { get; set; } = string.Empty;
        public virtual User User { get; set; } = null!;

        public RefreshToken()
        {
            Id = Guid.NewGuid();
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
```

### 2.4 Verification

```bash
dotnet build Domain/Domain.csproj
```

Should produce 0 errors, 0 warnings.

---

## Phase 3 — Persistence Layer

### 3.1 ApplicationDbContext

**`Persistence/Data/ApplicationDbContext.cs`**

```csharp
using Domain.Entity.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Data
{
    public class ApplicationDbContext : IdentityDbContext<
        User, Role, string,
        IdentityUserClaim<string>, UserRole, IdentityUserLogin<string>,
        IdentityRoleClaim<string>, IdentityUserToken<string>>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // Add domain DbSets here later, e.g.:
        // public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Apply all IEntityTypeConfiguration from this assembly
            builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

            // Strip "AspNet" prefix from Identity tables for cleaner names
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                var tableName = entityType.GetTableName();
                if (tableName != null && tableName.StartsWith("AspNet"))
                {
                    entityType.SetTableName(tableName.Substring(6)); // "AspNetUsers" → "Users"
                }
            }
        }
    }
}
```

### 3.2 Entity configurations

**`Persistence/Configurations/UserConfiguration.cs`**

```csharp
using Domain.Entity.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.Property(u => u.Status)
                .HasConversion<string>();

            builder.HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

**`Persistence/Configurations/RoleConfiguration.cs`**

```csharp
using Domain.Entity.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            // No additional configuration needed beyond Identity defaults
        }
    }
}
```

**`Persistence/Configurations/UserRoleConfiguration.cs`**

```csharp
using Domain.Entity.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
    {
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.HasKey(ur => new { ur.UserId, ur.RoleId });

            builder.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .IsRequired();

            builder.HasOne(ur => ur.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(ur => ur.RoleId)
                .IsRequired();
        }
    }
}
```

**`Persistence/Configurations/RefreshTokenConfiguration.cs`**

```csharp
using Domain.Entity.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Persistence.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(rt => rt.Id);

            builder.HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
```

### 3.3 Seeder

**`Persistence/Seeders/IDataSeeder.cs`**

```csharp
namespace Persistence.Seeders
{
    public interface IDataSeeder
    {
        Task SeedAsync();
    }
}
```

**`Persistence/Seeders/DataSeeder.cs`**

```csharp
using Domain.Entity.Identity;
using Domain.Enum;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Persistence.Seeders
{
    public class DataSeeder : IDataSeeder
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataSeeder> _logger;

        public DataSeeder(IServiceProvider serviceProvider, ILogger<DataSeeder> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

            // ── Seed roles ──
            // Adjust this list for your project: "Admin", "Customer", "Farmer", "Vendor", etc.
            string[] roles = ["Admin", "Customer", "Frontdesk"];
            foreach (var roleName in roles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new Role { Name = roleName });
                    _logger.LogInformation("Seeded role: {Role}", roleName);
                }
            }

            // ── Seed default admin ──
            const string adminEmail = "admin@myproject.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Admin",
                    Status = RegistrationStatus.Approved,
                    EmailConfirmed = true,
                    RequiresPasswordChange = false,
                    IsActive = true,
                };

                var result = await userManager.CreateAsync(admin, "Admin@123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                    _logger.LogInformation("Seeded admin user: {Email}", adminEmail);
                }
            }

            // Add domain-specific seeding here (e.g., CoworkingSeeder.SeedAsync())
        }
    }
}
```

### 3.4 Dependency injection registration

**`Persistence/DependencyInjection.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Persistence.Data;
using Persistence.Seeders;

namespace Persistence
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddPersistence(
            this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString)
                       .UseSnakeCaseNamingConvention());

            services.AddScoped<IDataSeeder, DataSeeder>();

            return services;
        }
    }
}
```

### 3.5 Verification

```bash
dotnet build Persistence/Persistence.csproj
```

---

## Phase 4 — Application Layer (CQRS, Validation, Pipeline)

### 4.1 Custom exceptions

**`Application/Common/Exceptions/NotFoundException.cs`**

```csharp
namespace Application.Common.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }
}
```

**`Application/Common/Exceptions/ConflictException.cs`**

```csharp
namespace Application.Common.Exceptions
{
    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }
}
```

**`Application/Common/Exceptions/UnauthorizedException.cs`**

```csharp
namespace Application.Common.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public UnauthorizedException(string message) : base(message) { }
    }
}
```

### 4.2 Pagination model

**`Application/Common/Models/PagedResult.cs`**

```csharp
namespace Application.Common.Models
{
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
```

### 4.3 Interfaces

**`Application/Common/Interfaces/ITokenService.cs`**

```csharp
using Domain.Entity.Identity;

namespace Application.Common.Interfaces
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user, string role);
        Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string jwtId);
        System.Security.Claims.ClaimsPrincipal GetPrincipalFromExpiredToken(string token);
    }
}
```

**`Application/Common/Interfaces/IStorageService.cs`**

```csharp
namespace Application.Common.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
        Task<Stream> DownloadFileAsync(string fileName);
        Task DeleteFileAsync(string fileName);
        Task<bool> FileExistsAsync(string fileName);
        Task<List<string>> ListFilesAsync(string? prefix = null);
        Task<string> GetBlobUrlWithSasAsync(string blobName, int expiryMinutes = 60);
        string GetBlobUrl(string blobName);
        Task<string> GetSasUrlFromAbsoluteUrlAsync(string absoluteUrl, int expiryMinutes = 60);
    }
}
```

**`Application/Common/Interfaces/IEmailService.cs`**

```csharp
namespace Application.Common.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string htmlBody);
        Task SendEmailAsync(IEnumerable<string> recipients, string subject, string htmlBody);
    }
}
```

### 4.4 MediatR pipeline behaviors

**`Application/Common/Behaviors/ValidationBehavior.cs`**

This runs ALL registered FluentValidation validators for each MediatR request before the handler executes.

```csharp
using FluentValidation;
using MediatR;

namespace Application.Common.Behaviors
{
    public class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

            var context = new ValidationContext<TRequest>(request);

            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);

            return await next();
        }
    }
}
```

**`Application/Common/Behaviors/LoggingBehavior.cs`**

```csharp
using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Application.Common.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoggingBehavior(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value ?? "anonymous";
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";
            var traceId = Activity.Current?.TraceId.ToString() ?? "no-trace";

            // Redact sensitive commands
            var isSensitive = requestName.Contains("Login", StringComparison.OrdinalIgnoreCase)
                           || requestName.Contains("Password", StringComparison.OrdinalIgnoreCase)
                           || requestName.Contains("Token", StringComparison.OrdinalIgnoreCase);
            var requestJson = isSensitive ? "[REDACTED]" : JsonSerializer.Serialize(request);

            Log.Information("[START] {RequestName} | User: {UserId} | IP: {IpAddress} | Trace: {TraceId} | Body: {RequestBody}",
                requestName, userId, ipAddress, traceId, requestJson);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next();
                stopwatch.Stop();

                if (stopwatch.ElapsedMilliseconds > 500)
                {
                    Log.Warning("[SLOW] {RequestName} took {ElapsedMs}ms | User: {UserId}",
                        requestName, stopwatch.ElapsedMilliseconds, userId);
                }

                Log.Information("[END] {RequestName} completed in {ElapsedMs}ms",
                    requestName, stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Log.Error("[ERROR] {RequestName} failed after {ElapsedMs}ms | Error: {ErrorMessage}",
                    requestName, stopwatch.ElapsedMilliseconds, ex.Message);
                throw;
            }
        }
    }
}
```

### 4.5 User mapping profile (AutoMapper)

**`Application/Services/User/UserMappingProfile.cs`**

```csharp
using Application.Services.User.Query.User;
using AutoMapper;

namespace Application.Services.User
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            CreateMap<Domain.Entity.Identity.User, UserDto>()
                .ForMember(dest => dest.UserId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber ?? string.Empty));
        }
    }
}
```

**`Application/Services/User/Query/User/UserDto.cs`**

```csharp
using Domain.Enum;

namespace Application.Services.User.Query.User
{
    public class UserDto
    {
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? MiddleName { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public RegistrationStatus Status { get; set; }
        public bool RequiresPasswordChange { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
```

### 4.6 Dependency injection registration

**`Application/DependencyInjection.cs`**

```csharp
using Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var assembly = typeof(DependencyInjection).Assembly;

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

            // Pipeline order matters: logging first, then validation
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            services.AddValidatorsFromAssembly(assembly);
            services.AddAutoMapper(assembly);

            return services;
        }
    }
}
```

### 4.7 Verification

```bash
dotnet build Application/Application.csproj
```

---

## Phase 5 — Infrastructure Layer (Tokens, Storage, Email)

### 5.1 Token service

**`Infrastructure/Services/TokenService.cs`**

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Application.Common.Interfaces;
using Domain.Entity.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Persistence.Data;

namespace Infrastructure.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _dbContext;

        public TokenService(IConfiguration configuration, ApplicationDbContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public string GenerateAccessToken(User user, string role)
        {
            var jwtId = Guid.NewGuid().ToString();

            var claims = new List<Claim>
            {
                new("sub", user.Id),
                new("email", user.Email ?? string.Empty),
                new("name", $"{user.FirstName} {user.LastName}"),
                new(ClaimTypes.Role, role),
                new(JwtRegisteredClaimNames.Jti, jwtId),
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    double.Parse(_configuration["Jwt:ExpiryMinutes"]!)),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string jwtId)
        {
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                JwtId = jwtId,
                UserId = userId,
                ExpiryDate = DateTime.UtcNow.AddDays(
                    double.Parse(_configuration["Jwt:RefreshTokenExpiryDays"]!)),
                Used = false,
                Invalidated = false,
            };

            await _dbContext.Set<RefreshToken>().AddAsync(refreshToken);
            await _dbContext.SaveChangesAsync();

            return refreshToken;
        }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token)
        {
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // Allow expired tokens
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"]!)),
                MapInboundClaims = false,
            };

            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, tokenValidationParameters, out var securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(
                    SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new SecurityTokenException("Invalid token");
            }

            return principal;
        }
    }
}
```

### 5.2 Storage service (Azure Blob)

**`Infrastructure/Configuration/StorageConfiguration.cs`**

```csharp
namespace Infrastructure.Configuration
{
    public class StorageConfiguration
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
    }
}
```

**`Infrastructure/Services/StorageService.cs`**

```csharp
using Application.Common.Interfaces;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Infrastructure.Configuration;

namespace Infrastructure.Services
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly StorageConfiguration _config;

        public AzureBlobStorageService(StorageConfiguration config)
        {
            _config = config;
            var blobServiceClient = new BlobServiceClient(config.ConnectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(config.ContainerName);
            _containerClient.CreateIfNotExists(PublicAccessType.None);
        }

        public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
        {
            var blobClient = _containerClient.GetBlobClient(fileName);
            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = headers });
            return blobClient.Uri.ToString();
        }

        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            var blobClient = _containerClient.GetBlobClient(fileName);
            var response = await blobClient.DownloadAsync();
            var memoryStream = new MemoryStream();
            await response.Value.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }

        public async Task DeleteFileAsync(string fileName)
        {
            var blobClient = _containerClient.GetBlobClient(fileName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task<bool> FileExistsAsync(string fileName)
        {
            var blobClient = _containerClient.GetBlobClient(fileName);
            return await blobClient.ExistsAsync();
        }

        public async Task<List<string>> ListFilesAsync(string? prefix = null)
        {
            var files = new List<string>();
            await foreach (var blob in _containerClient.GetBlobsAsync(prefix: prefix))
            {
                files.Add(blob.Name);
            }
            return files;
        }

        public async Task<string> GetBlobUrlWithSasAsync(string blobName, int expiryMinutes = 60)
        {
            var blobClient = _containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                throw new FileNotFoundException($"Blob '{blobName}' not found.");

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _config.ContainerName,
                BlobName = blobName,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes),
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }

        public string GetBlobUrl(string blobName)
        {
            return _containerClient.GetBlobClient(blobName).Uri.ToString();
        }

        public async Task<string> GetSasUrlFromAbsoluteUrlAsync(string absoluteUrl, int expiryMinutes = 60)
        {
            var containerBaseUrl = _containerClient.Uri.ToString().TrimEnd('/') + "/";
            if (!absoluteUrl.StartsWith(containerBaseUrl))
                throw new ArgumentException("URL does not belong to this container.");

            var blobName = absoluteUrl.Substring(containerBaseUrl.Length);
            return await GetBlobUrlWithSasAsync(blobName, expiryMinutes);
        }
    }
}
```

### 5.3 Email service

**`Infrastructure/Services/EmailService.cs`**

```csharp
using Application.Common.Interfaces;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailClient _emailClient;
        private readonly string _senderAddress;

        public EmailService(IConfiguration configuration)
        {
            _emailClient = new EmailClient(
                configuration["AzureCommunication:ConnectionString"]);
            _senderAddress = configuration["AzureCommunication:SenderAddress"]!;
        }

        public async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            await _emailClient.SendAsync(
                Azure.WaitUntil.Completed,
                _senderAddress, to, subject, htmlBody);
        }

        public async Task SendEmailAsync(
            IEnumerable<string> recipients, string subject, string htmlBody)
        {
            foreach (var recipient in recipients)
            {
                await SendEmailAsync(recipient, subject, htmlBody);
            }
        }
    }
}
```

### 5.4 Dependency injection registration

**`Infrastructure/DependencyInjection.cs`**

```csharp
using Application.Common.Interfaces;
using Infrastructure.Configuration;
using Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            string storageConnectionString,
            string storageContainerName)
        {
            // Storage
            var storageConfig = new StorageConfiguration
            {
                ConnectionString = storageConnectionString,
                ContainerName = storageContainerName,
            };
            services.AddSingleton(storageConfig);
            services.AddScoped<IStorageService, AzureBlobStorageService>();

            // Auth tokens
            services.AddScoped<ITokenService, TokenService>();

            // Email
            services.AddScoped<IEmailService, EmailService>();

            return services;
        }
    }
}
```

### 5.5 Verification

```bash
dotnet build Infrastructure/Infrastructure.csproj
```

---

## Phase 6 — WebApi Layer (DI, Middleware, Program.cs)

### 6.1 Global exception handler

**`WebApi/Infrastructure/GlobalExceptionHandler.cs`**

This maps all custom exceptions to proper HTTP ProblemDetails responses.

```csharp
using Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Infrastructure
{
    public class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            var (statusCode, title, detail, errors) = exception switch
            {
                ValidationException ve => (
                    StatusCodes.Status400BadRequest,
                    "ValidationException",
                    "One or more validation errors occurred.",
                    ve.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()
                        ) as object
                ),
                UnauthorizedException ue => (
                    StatusCodes.Status401Unauthorized,
                    "UnauthorizedException",
                    ue.Message,
                    null as object
                ),
                NotFoundException nfe => (
                    StatusCodes.Status404NotFound,
                    "NotFoundException",
                    nfe.Message,
                    null as object
                ),
                ConflictException ce => (
                    StatusCodes.Status409Conflict,
                    "ConflictException",
                    ce.Message,
                    null as object
                ),
                ArgumentException ae => (
                    StatusCodes.Status400BadRequest,
                    "ArgumentException",
                    ae.Message,
                    null as object
                ),
                InvalidOperationException ioe => (
                    StatusCodes.Status400BadRequest,
                    "InvalidOperationException",
                    ioe.Message,
                    null as object
                ),
                _ => (
                    StatusCodes.Status500InternalServerError,
                    "InternalServerError",
                    "An unexpected error occurred.",
                    null as object
                )
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{statusCode}",
            };

            if (errors != null)
            {
                problemDetails.Extensions["errors"] = errors;
            }

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            return true;
        }
    }
}
```

### 6.2 WebApi dependency injection

**`WebApi/DependencyInjection.cs`**

```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using WebApi.Infrastructure;

namespace DependencyInjection
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddWebApi(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // ── Controllers + JSON ──
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Converters.Add(
                        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
                });

            // ── Global error handling ──
            services.AddProblemDetails();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddHttpContextAccessor();

            // ── JWT Authentication ──
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
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    // CRITICAL: Map role claims correctly for [Authorize(Roles = "...")]
                    RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
                    NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
                };
            });

            services.AddAuthorization();

            // ── Swagger with Bearer auth ──
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "My Project API",
                    Version = "v1"
                });

                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token",
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            // ── Rate limiting ──
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddFixedWindowLimiter("default", limiter =>
                {
                    limiter.PermitLimit = 10;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                });
            });

            // ── CORS ──
            services.AddCors(options =>
            {
                options.AddPolicy("Cors", policy =>
                {
                    policy.WithOrigins(
                        "http://localhost:5173",  // Vite dev server
                        "https://localhost:7129"  // .NET dev server
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials(); // Required for HttpOnly cookies
                });
            });

            // ── Health checks ──
            services.AddHealthChecks();

            return services;
        }

        public static void AddSerilogLogging(this WebApplicationBuilder builder)
        {
            builder.Host.UseSerilog((context, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadId()
                    .Enrich.WithEnvironmentName()
                    .Enrich.FromLogContext()
                    .WriteTo.Console(theme: AnsiConsoleTheme.Code)
                    .WriteTo.File(
                        "Logs/log-.txt",
                        rollingInterval: RollingInterval.Day,
                        restrictedToMinimumLevel: LogEventLevel.Information);
            });
        }
    }
}
```

### 6.3 appsettings.json

**`WebApi/appsettings.json`**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Jwt": {
    "Issuer": "my-project-api",
    "Audience": "my-project-client"
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

### 6.4 Program.cs

**`WebApi/Program.cs`**

```csharp
using DependencyInjection;
using Domain.Entity.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence;
using Persistence.Data;
using Persistence.Seeders;

// ── Load .env file ──
DotNetEnv.Env.Load(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"));

var builder = WebApplication.CreateBuilder(args);

// ── Build connection string from env vars ──
var connectionString =
    $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
    $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
    $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
    $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
    $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")}";

// ── Override config with env vars ──
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;
builder.Configuration["Jwt:Secret"] = Environment.GetEnvironmentVariable("JWT_SECRET");
builder.Configuration["Jwt:ExpiryMinutes"] = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES");
builder.Configuration["Jwt:RefreshTokenExpiryDays"] = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY_DAYS");
builder.Configuration["AzureCommunication:ConnectionString"] = Environment.GetEnvironmentVariable("AZURE_COMMUNICATION_CONNECTION_STRING");
builder.Configuration["AzureCommunication:SenderAddress"] = Environment.GetEnvironmentVariable("AZURE_EMAIL_SENDER");

// ── Register services (order matters) ──
builder.Services.AddPersistence(connectionString);

builder.Services.AddIdentity<User, Role>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 10;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddWebApi(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(
    Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING") ?? "",
    Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME") ?? "");

builder.AddSerilogLogging();

var app = builder.Build();

// ── Auto-migrate & seed ──
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.SeedAsync();
}

// ── Middleware pipeline (order matters) ──
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseStatusCodePages();
app.UseCors("Cors");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();  // Must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
```

### 6.5 Create initial migration and verify

```bash
# From the solution root:
dotnet ef migrations add InitialCreate --project Persistence --startup-project WebApi

# Run the app (this will apply migrations and seed data)
dotnet run --project WebApi
```

### 6.6 Verification

- `GET /health` returns 200
- Swagger UI loads at `/swagger`
- Database tables exist with seeded roles and admin user

---

## Phase 7 — Auth System (Login, Refresh, Logout, Change/Reset Password)

### 7.1 Request DTOs

**`Application/Services/User/Request/LoginRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Request/RefreshTokenRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class RefreshTokenRequest
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Request/ChangePasswordRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Request/ResetPasswordRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}
```

### 7.2 Response DTOs

**`Application/Services/User/Response/LoginResponse.cs`**

```csharp
namespace Application.Services.User.Response
{
    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool RequiresPasswordChange { get; set; }

        // RefreshToken is NOT included here — it goes in an HttpOnly cookie
    }
}
```

**`Application/Services/User/Response/RefreshTokenResponse.cs`**

```csharp
namespace Application.Services.User.Response
{
    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }
}
```

### 7.3 Commands

**`Application/Services/User/Command/LoginCommand.cs`**

```csharp
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Services.User.Request;
using Application.Services.User.Response;
using Domain.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    // ── Command ──
    public class LoginCommand : IRequest<(LoginResponse Response, string RefreshToken)>
    {
        public LoginRequest Request { get; }
        public LoginCommand(LoginRequest request) => Request = request;
    }

    // ── Handler ──
    public class LoginCommandHandler
        : IRequestHandler<LoginCommand, (LoginResponse Response, string RefreshToken)>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly ITokenService _tokenService;

        public LoginCommandHandler(
            UserManager<Domain.Entity.Identity.User> userManager,
            ITokenService tokenService)
        {
            _userManager = userManager;
            _tokenService = tokenService;
        }

        public async Task<(LoginResponse Response, string RefreshToken)> Handle(
            LoginCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByEmailAsync(command.Request.Email)
                ?? throw new UnauthorizedException("Invalid email or password.");

            if (!user.IsActive)
                throw new UnauthorizedException("Account has been deactivated.");

            if (!await _userManager.CheckPasswordAsync(user, command.Request.Password))
                throw new UnauthorizedException("Invalid email or password.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Customer";

            var accessToken = _tokenService.GenerateAccessToken(user, role);

            // Extract jti from access token to link it with the refresh token
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(accessToken);
            var jti = jwtToken.Claims.First(c => c.Type == "jti").Value;

            var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, jti);

            var response = new LoginResponse
            {
                AccessToken = accessToken,
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = role,
                RequiresPasswordChange = user.RequiresPasswordChange,
            };

            return (response, refreshToken.Token);
        }
    }
}
```

**`Application/Services/User/Command/RefreshTokenCommand.cs`**

This implements **rotating refresh tokens with breach detection**. If a previously-used token is presented, ALL tokens for that user are invalidated (security measure against token theft).

```csharp
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Services.User.Request;
using Application.Services.User.Response;
using Domain.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.User.Command
{
    public class RefreshTokenCommand : IRequest<(RefreshTokenResponse Response, string NewRefreshToken)>
    {
        public string AccessToken { get; }
        public string RefreshToken { get; }
        public RefreshTokenCommand(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }
    }

    public class RefreshTokenCommandHandler
        : IRequestHandler<RefreshTokenCommand, (RefreshTokenResponse Response, string NewRefreshToken)>
    {
        private readonly ITokenService _tokenService;
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public RefreshTokenCommandHandler(
            ITokenService tokenService,
            UserManager<Domain.Entity.Identity.User> userManager,
            ApplicationDbContext dbContext)
        {
            _tokenService = tokenService;
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public async Task<(RefreshTokenResponse Response, string NewRefreshToken)> Handle(
            RefreshTokenCommand command, CancellationToken cancellationToken)
        {
            // 1. Validate the expired access token structure (not lifetime)
            var principal = _tokenService.GetPrincipalFromExpiredToken(command.AccessToken);
            var jti = principal.Claims.FirstOrDefault(c => c.Type == "jti")?.Value
                ?? throw new UnauthorizedException("Invalid access token.");

            // 2. Find the stored refresh token
            var storedToken = await _dbContext.Set<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.Token == command.RefreshToken, cancellationToken)
                ?? throw new UnauthorizedException("Refresh token not found.");

            // 3. Breach detection: if token was already used, invalidate ALL tokens for this user
            if (storedToken.Used)
            {
                var userTokens = await _dbContext.Set<RefreshToken>()
                    .Where(rt => rt.UserId == storedToken.UserId)
                    .ToListAsync(cancellationToken);

                foreach (var token in userTokens)
                {
                    token.Invalidated = true;
                    token.UpdatedAt = DateTime.UtcNow;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);

                throw new UnauthorizedException("Refresh token has been revoked.");
            }

            // 4. Standard validation
            if (storedToken.Invalidated)
                throw new UnauthorizedException("Refresh token has been revoked.");
            if (storedToken.ExpiryDate < DateTime.UtcNow)
                throw new UnauthorizedException("Refresh token has expired.");
            if (storedToken.JwtId != jti)
                throw new UnauthorizedException("Refresh token does not match the access token.");

            // 5. Mark current token as used (rotation)
            storedToken.Used = true;
            storedToken.UpdatedAt = DateTime.UtcNow;

            // 6. Issue new token pair
            var userId = principal.Claims.First(c => c.Type == "sub").Value;
            var user = await _userManager.FindByIdAsync(userId)
                ?? throw new UnauthorizedException("User not found.");

            if (!user.IsActive)
                throw new UnauthorizedException("Account has been deactivated.");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Customer";

            var newAccessToken = _tokenService.GenerateAccessToken(user, role);
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var newJwtToken = handler.ReadJwtToken(newAccessToken);
            var newJti = newJwtToken.Claims.First(c => c.Type == "jti").Value;

            var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, newJti);

            await _dbContext.SaveChangesAsync(cancellationToken);

            return (new RefreshTokenResponse { AccessToken = newAccessToken }, newRefreshToken.Token);
        }
    }
}
```

**`Application/Services/User/Command/LogoutCommand.cs`**

```csharp
using Application.Common.Exceptions;
using Domain.Entity.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;

namespace Application.Services.User.Command
{
    public class LogoutCommand : IRequest<string>
    {
        public string RefreshToken { get; }
        public LogoutCommand(string refreshToken) => RefreshToken = refreshToken;
    }

    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, string>
    {
        private readonly ApplicationDbContext _dbContext;

        public LogoutCommandHandler(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<string> Handle(LogoutCommand command, CancellationToken cancellationToken)
        {
            var storedToken = await _dbContext.Set<RefreshToken>()
                .FirstOrDefaultAsync(rt => rt.Token == command.RefreshToken, cancellationToken)
                ?? throw new NotFoundException("Session not found.");

            storedToken.Invalidated = true;
            storedToken.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return "Logged out successfully.";
        }
    }
}
```

**`Application/Services/User/Command/ChangePasswordCommand.cs`**

```csharp
using Application.Common.Exceptions;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class ChangePasswordCommand : IRequest<string>
    {
        public string UserId { get; }
        public ChangePasswordRequest Request { get; }

        public ChangePasswordCommand(string userId, ChangePasswordRequest request)
        {
            UserId = userId;
            Request = request;
        }
    }

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, string>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public ChangePasswordCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<string> Handle(
            ChangePasswordCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.UserId)
                ?? throw new NotFoundException("User not found.");

            var result = await _userManager.ChangePasswordAsync(
                user, command.Request.CurrentPassword, command.Request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new ConflictException($"Password change failed: {errors}");
            }

            // Clear the flag so the user isn't prompted again
            user.RequiresPasswordChange = false;
            await _userManager.UpdateAsync(user);

            return "Password changed successfully.";
        }
    }
}
```

**`Application/Services/User/Command/ResetPasswordCommand.cs`**

Admin-only: resets another user's password and forces them to change it on next login.

```csharp
using Application.Common.Exceptions;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class ResetPasswordCommand : IRequest<string>
    {
        public string UserId { get; }
        public ResetPasswordRequest Request { get; }

        public ResetPasswordCommand(string userId, ResetPasswordRequest request)
        {
            UserId = userId;
            Request = request;
        }
    }

    public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, string>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public ResetPasswordCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<string> Handle(
            ResetPasswordCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.UserId)
                ?? throw new NotFoundException("User not found.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(
                user, token, command.Request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new ConflictException($"Password reset failed: {errors}");
            }

            user.RequiresPasswordChange = true;
            await _userManager.UpdateAsync(user);

            return "Password reset successfully. User must change password on next login.";
        }
    }
}
```

### 7.4 Validators

**`Application/Services/User/Validator/LoginCommandValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class LoginCommandValidator : AbstractValidator<LoginCommand>
    {
        public LoginCommandValidator()
        {
            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.Request.Password)
                .NotEmpty().WithMessage("Password is required.");
        }
    }
}
```

**`Application/Services/User/Validator/RefreshTokenCommandValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.AccessToken)
                .NotEmpty().WithMessage("Access token is required.");

            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh token is required.");
        }
    }
}
```

**`Application/Services/User/Validator/ChangePasswordCommandValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required.");

            RuleFor(x => x.Request.CurrentPassword)
                .NotEmpty().WithMessage("Current password is required.");

            RuleFor(x => x.Request.NewPassword)
                .NotEmpty().WithMessage("New password is required.")
                .MinimumLength(10).WithMessage("Password must be at least 10 characters.")
                .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain a digit.");

            RuleFor(x => x.Request.ConfirmNewPassword)
                .Equal(x => x.Request.NewPassword).WithMessage("Passwords do not match.");
        }
    }
}
```

**`Application/Services/User/Validator/ResetPasswordCommandValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
    {
        public ResetPasswordCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required.");

            RuleFor(x => x.Request.NewPassword)
                .NotEmpty().WithMessage("New password is required.")
                .MinimumLength(10).WithMessage("Password must be at least 10 characters.")
                .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
                .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
                .Matches("[0-9]").WithMessage("Password must contain a digit.");

            RuleFor(x => x.Request.ConfirmNewPassword)
                .Equal(x => x.Request.NewPassword).WithMessage("Passwords do not match.");
        }
    }
}
```

### 7.5 Auth controller

**`WebApi/Controllers/AuthController.cs`**

```csharp
using Application.Services.User.Command;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var (response, refreshToken) = await _mediator.Send(new LoginCommand(request));

            // Set refresh token as HttpOnly cookie — never exposed to JavaScript
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
            });

            return Ok(response);
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            var refreshToken = Request.Cookies["refreshToken"]
                ?? throw new Application.Common.Exceptions.UnauthorizedException(
                    "No refresh token provided.");

            var (response, newRefreshToken) = await _mediator.Send(
                new RefreshTokenCommand(request.AccessToken, refreshToken));

            Response.Cookies.Append("refreshToken", newRefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(7),
            });

            return Ok(response);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (string.IsNullOrEmpty(refreshToken))
                return Ok(new { message = "Already logged out." });

            var result = await _mediator.Send(new LogoutCommand(refreshToken));

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });

            return Ok(new { message = result });
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = User.FindFirst("sub")?.Value
                ?? throw new Application.Common.Exceptions.UnauthorizedException(
                    "User not authenticated.");

            var result = await _mediator.Send(new ChangePasswordCommand(userId, request));
            return Ok(new { message = result });
        }
    }
}
```

### 7.6 Verification

```bash
dotnet build WebApi/WebApi.csproj
dotnet run --project WebApi
```

Test:
1. `POST /api/auth/login` with `{"email":"admin@myproject.com","password":"Admin@123!"}` → 200 + access token + refreshToken cookie
2. `POST /api/auth/refresh` with expired access token in body → 200 + rotated tokens
3. `POST /api/auth/logout` → invalidates token + clears cookie
4. `POST /api/auth/change-password` with Bearer token → changes password

---

## Phase 8 — Role-Based User CRUD (Admin + Generic Role Pattern)

This phase implements a **generic, role-agnostic pattern** for listing users by role, plus role-specific creation commands. To add a new user type (e.g., "Farmer", "Vendor"), you only need to:

1. Create a new `Create{Role}Command` (copy the pattern)
2. Create the request/response DTOs
3. Create a new controller with the role-specific create endpoint
4. Reuse the generic `UpdateUserCommand`, `DeleteUserCommand`, and role-based queries

### 8.1 Generic user commands (shared across all roles)

**`Application/Services/User/Request/UpdateUserRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
    }
}
```

**`Application/Services/User/Command/UpdateUserCommand.cs`**

```csharp
using Application.Common.Exceptions;
using Application.Services.User.Request;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class UpdateUserCommand : IRequest<UpdateUserResponse>
    {
        public string UserId { get; }
        public UpdateUserRequest Request { get; }

        public UpdateUserCommand(string userId, UpdateUserRequest request)
        {
            UserId = userId;
            Request = request;
        }
    }

    public class UpdateUserCommandHandler
        : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public UpdateUserCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<UpdateUserResponse> Handle(
            UpdateUserCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.UserId)
                ?? throw new NotFoundException($"User with ID '{command.UserId}' not found.");

            if (!user.IsActive)
                throw new ConflictException("Cannot edit a deactivated user.");

            // Only update fields that were provided
            if (command.Request.FirstName != null) user.FirstName = command.Request.FirstName;
            if (command.Request.LastName != null) user.LastName = command.Request.LastName;
            if (command.Request.MiddleName != null) user.MiddleName = command.Request.MiddleName;
            if (command.Request.PhoneNumber != null) user.PhoneNumber = command.Request.PhoneNumber;
            if (command.Request.Address != null) user.Address = command.Request.Address;

            if (command.Request.Email != null && command.Request.Email != user.Email)
            {
                var existingUser = await _userManager.FindByEmailAsync(command.Request.Email);
                if (existingUser != null && existingUser.Id != user.Id)
                    throw new ConflictException("Email is already in use.");

                user.Email = command.Request.Email;
                user.UserName = command.Request.Email;
                user.NormalizedEmail = command.Request.Email.ToUpper();
                user.NormalizedUserName = command.Request.Email.ToUpper();
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new ConflictException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            return new UpdateUserResponse
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Message = "User updated successfully.",
            };
        }
    }
}
```

**`Application/Services/User/Response/UpdateUserResponse.cs`**

```csharp
namespace Application.Services.User.Response
{
    public class UpdateUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Command/DeleteUserCommand.cs`**

Soft-delete: sets `IsActive = false` and locks the account permanently.

```csharp
using Application.Common.Exceptions;
using Application.Services.User.Response;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class DeleteUserCommand : IRequest<DeleteUserResponse>
    {
        public string UserId { get; }
        public DeleteUserCommand(string userId) => UserId = userId;
    }

    public class DeleteUserCommandHandler
        : IRequestHandler<DeleteUserCommand, DeleteUserResponse>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;

        public DeleteUserCommandHandler(UserManager<Domain.Entity.Identity.User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<DeleteUserResponse> Handle(
            DeleteUserCommand command, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(command.UserId)
                ?? throw new NotFoundException($"User with ID '{command.UserId}' not found.");

            if (!user.IsActive)
                throw new ConflictException("User is already deactivated.");

            // Soft delete: deactivate + lock out permanently
            user.IsActive = false;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.LockoutEnabled = true;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
                throw new ConflictException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            return new DeleteUserResponse
            {
                UserId = user.Id,
                Message = "User deactivated successfully.",
            };
        }
    }
}
```

**`Application/Services/User/Response/DeleteUserResponse.cs`**

```csharp
namespace Application.Services.User.Response
{
    public class DeleteUserResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
```

### 8.2 Generic role-based queries

These queries work for ANY role — Admin, Frontdesk, Farmer, Vendor, etc. You never need role-specific query classes.

**`Application/Services/User/Query/User/GetActiveUsersByRoleQuery.cs`**

```csharp
using Application.Services.User.Query.User;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.User.Query
{
    public class GetActiveUsersByRoleQuery : IRequest<List<UserDto>>
    {
        public string Role { get; }
        public GetActiveUsersByRoleQuery(string role) => Role = role;
    }

    public class GetActiveUsersByRoleQueryHandler
        : IRequestHandler<GetActiveUsersByRoleQuery, List<UserDto>>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly IMapper _mapper;

        public GetActiveUsersByRoleQueryHandler(
            UserManager<Domain.Entity.Identity.User> userManager, IMapper mapper)
        {
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<List<UserDto>> Handle(
            GetActiveUsersByRoleQuery query, CancellationToken cancellationToken)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(query.Role);
            var activeUsers = usersInRole
                .Where(u => u.IsActive)
                .OrderByDescending(u => u.Id)
                .ToList();

            return _mapper.Map<List<UserDto>>(activeUsers);
        }
    }
}
```

**`Application/Services/User/Query/User/GetArchivedUsersByRoleQuery.cs`**

```csharp
using Application.Services.User.Query.User;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Query
{
    public class GetArchivedUsersByRoleQuery : IRequest<List<UserDto>>
    {
        public string Role { get; }
        public GetArchivedUsersByRoleQuery(string role) => Role = role;
    }

    public class GetArchivedUsersByRoleQueryHandler
        : IRequestHandler<GetArchivedUsersByRoleQuery, List<UserDto>>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly IMapper _mapper;

        public GetArchivedUsersByRoleQueryHandler(
            UserManager<Domain.Entity.Identity.User> userManager, IMapper mapper)
        {
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<List<UserDto>> Handle(
            GetArchivedUsersByRoleQuery query, CancellationToken cancellationToken)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(query.Role);
            var archivedUsers = usersInRole
                .Where(u => !u.IsActive)
                .OrderByDescending(u => u.Id)
                .ToList();

            return _mapper.Map<List<UserDto>>(archivedUsers);
        }
    }
}
```

**`Application/Services/User/Query/User/GetUserByIdQuery.cs`**

```csharp
using Application.Common.Exceptions;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Query.User
{
    public class GetUserByIdQuery : IRequest<UserDto>
    {
        public string UserId { get; }
        public GetUserByIdQuery(string userId) => UserId = userId;
    }

    public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDto>
    {
        private readonly UserManager<Domain.Entity.Identity.User> _userManager;
        private readonly IMapper _mapper;

        public GetUserByIdQueryHandler(
            UserManager<Domain.Entity.Identity.User> userManager, IMapper mapper)
        {
            _userManager = userManager;
            _mapper = mapper;
        }

        public async Task<UserDto> Handle(
            GetUserByIdQuery query, CancellationToken cancellationToken)
        {
            var user = await _userManager.FindByIdAsync(query.UserId)
                ?? throw new NotFoundException($"User with ID '{query.UserId}' not found.");

            var dto = _mapper.Map<UserDto>(user);
            dto.CreatedAt = DateTime.SpecifyKind(dto.CreatedAt, DateTimeKind.Utc);
            return dto;
        }
    }
}
```

### 8.3 Role-specific create commands

Each role has its own create command because roles may have different validation rules, default values, or side effects. Here are the two from the reference project:

**`Application/Services/User/Request/CreateAdminRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class CreateAdminRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Response/CreateAdminResponse.cs`**

```csharp
namespace Application.Services.User.Response
{
    public class CreateAdminResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Command/CreateAdminCommand.cs`**

```csharp
using Application.Common.Exceptions;
using Application.Services.User.Request;
using Application.Services.User.Response;
using Domain.Entity.Identity;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class CreateAdminCommand : IRequest<CreateAdminResponse>
    {
        public CreateAdminRequest Request { get; }
        public CreateAdminCommand(CreateAdminRequest request) => Request = request;
    }

    public class CreateAdminCommandHandler
        : IRequestHandler<CreateAdminCommand, CreateAdminResponse>
    {
        private readonly UserManager<User> _userManager;

        public CreateAdminCommandHandler(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<CreateAdminResponse> Handle(
            CreateAdminCommand command, CancellationToken cancellationToken)
        {
            var existing = await _userManager.FindByEmailAsync(command.Request.Email);
            if (existing != null)
                throw new ConflictException("A user with this email already exists.");

            var user = new User
            {
                UserName = command.Request.Email,
                Email = command.Request.Email,
                FirstName = command.Request.FirstName,
                LastName = command.Request.LastName,
                PhoneNumber = command.Request.PhoneNumber,
                Status = RegistrationStatus.Approved,
                EmailConfirmed = true,
                RequiresPasswordChange = true,  // Must change on first login
                IsActive = true,
            };

            var result = await _userManager.CreateAsync(user, command.Request.Password);
            if (!result.Succeeded)
                throw new ConflictException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            await _userManager.AddToRoleAsync(user, "Admin");

            return new CreateAdminResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Message = "Admin created successfully.",
            };
        }
    }
}
```

**`Application/Services/User/Validator/CreateAdminCommandValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class CreateAdminCommandValidator : AbstractValidator<CreateAdminCommand>
    {
        public CreateAdminCommandValidator()
        {
            RuleFor(x => x.Request.FirstName)
                .NotEmpty().WithMessage("First name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.LastName)
                .NotEmpty().WithMessage("Last name is required.")
                .MaximumLength(100);

            RuleFor(x => x.Request.Email)
                .NotEmpty().WithMessage("Email is required.")
                .EmailAddress().WithMessage("Invalid email format.");

            RuleFor(x => x.Request.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(10).WithMessage("Password must be at least 10 characters.")
                .Matches("[A-Z]").WithMessage("Must contain an uppercase letter.")
                .Matches("[a-z]").WithMessage("Must contain a lowercase letter.")
                .Matches("[0-9]").WithMessage("Must contain a digit.");

            RuleFor(x => x.Request.ConfirmPassword)
                .Equal(x => x.Request.Password).WithMessage("Passwords do not match.");

            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().WithMessage("Phone number is required.")
                .Length(10).WithMessage("Phone number must be 10 digits.")
                .Matches("^[0-9]+$").WithMessage("Phone number must contain only digits.");
        }
    }
}
```

### 8.4 Adding a new role (e.g., Frontdesk, Farmer, Vendor)

To add a new role-specific user type, copy the Admin pattern with these changes:

1. **Seed the role** in `DataSeeder.cs` — add the role name to the `roles` array
2. **Create request/response/command/validator** files — copy from Admin, replace:
   - Class names: `CreateAdmin*` → `Create{Role}*`
   - Role string: `"Admin"` → `"{Role}"` (must match the seeded role name exactly)
3. **Create a controller** — same structure, adjust route prefix and role authorization

Example for "Frontdesk":

**`Application/Services/User/Request/CreateFrontDeskRequest.cs`**

```csharp
namespace Application.Services.User.Request
{
    public class CreateFrontDeskRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Response/CreateFrontDeskResponse.cs`**

```csharp
namespace Application.Services.User.Response
{
    public class CreateFrontDeskResponse
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
```

**`Application/Services/User/Command/CreateFrontDeskCommand.cs`**

```csharp
using Application.Common.Exceptions;
using Application.Services.User.Request;
using Application.Services.User.Response;
using Domain.Entity.Identity;
using Domain.Enum;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Application.Services.User.Command
{
    public class CreateFrontDeskCommand : IRequest<CreateFrontDeskResponse>
    {
        public CreateFrontDeskRequest Request { get; }
        public CreateFrontDeskCommand(CreateFrontDeskRequest request) => Request = request;
    }

    public class CreateFrontDeskCommandHandler
        : IRequestHandler<CreateFrontDeskCommand, CreateFrontDeskResponse>
    {
        private readonly UserManager<User> _userManager;

        public CreateFrontDeskCommandHandler(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        public async Task<CreateFrontDeskResponse> Handle(
            CreateFrontDeskCommand command, CancellationToken cancellationToken)
        {
            var existing = await _userManager.FindByEmailAsync(command.Request.Email);
            if (existing != null)
                throw new ConflictException("A user with this email already exists.");

            var user = new User
            {
                UserName = command.Request.Email,
                Email = command.Request.Email,
                FirstName = command.Request.FirstName,
                LastName = command.Request.LastName,
                PhoneNumber = command.Request.PhoneNumber,
                Status = RegistrationStatus.Approved,
                EmailConfirmed = true,
                RequiresPasswordChange = true,
                IsActive = true,
            };

            var result = await _userManager.CreateAsync(user, command.Request.Password);
            if (!result.Succeeded)
                throw new ConflictException(
                    string.Join("; ", result.Errors.Select(e => e.Description)));

            // IMPORTANT: Role name must match EXACTLY what was seeded (case-sensitive)
            await _userManager.AddToRoleAsync(user, "Frontdesk");

            return new CreateFrontDeskResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Message = "Frontdesk user created successfully.",
            };
        }
    }
}
```

**`Application/Services/User/Validator/CreateFrontDeskCommandValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class CreateFrontDeskCommandValidator : AbstractValidator<CreateFrontDeskCommand>
    {
        public CreateFrontDeskCommandValidator()
        {
            // Same validation rules as Admin — adjust per role if needed
            RuleFor(x => x.Request.FirstName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Request.LastName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Request.Password)
                .NotEmpty().MinimumLength(10)
                .Matches("[A-Z]").Matches("[a-z]").Matches("[0-9]");
            RuleFor(x => x.Request.ConfirmPassword)
                .Equal(x => x.Request.Password).WithMessage("Passwords do not match.");
            RuleFor(x => x.Request.PhoneNumber)
                .NotEmpty().Length(10).Matches("^[0-9]+$");
        }
    }
}
```

### 8.5 Admin controller

**`WebApi/Controllers/AdminController.cs`**

```csharp
using Application.Services.User.Command;
using Application.Services.User.Query;
using Application.Services.User.Query.User;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AdminController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest request)
        {
            var result = await _mediator.Send(new CreateAdminCommand(request));
            return Ok(result);
        }

        [HttpPut("edit-admin/{id}")]
        public async Task<IActionResult> EditAdmin(string id, [FromBody] UpdateUserRequest request)
        {
            var result = await _mediator.Send(new UpdateUserCommand(id, request));
            return Ok(result);
        }

        [HttpDelete("delete-admin/{id}")]
        public async Task<IActionResult> DeleteAdmin(string id)
        {
            var result = await _mediator.Send(new DeleteUserCommand(id));
            return Ok(result);
        }

        [HttpGet("get-admin/{id}")]
        public async Task<IActionResult> GetAdminById(string id)
        {
            var result = await _mediator.Send(new GetUserByIdQuery(id));
            return Ok(result);
        }

        [HttpGet("active-admins")]
        public async Task<IActionResult> GetAllActiveAdmins()
        {
            var result = await _mediator.Send(new GetActiveUsersByRoleQuery("Admin"));
            return Ok(result);
        }

        [HttpGet("archived-admins")]
        public async Task<IActionResult> GetAllArchivedAdmins()
        {
            var result = await _mediator.Send(new GetArchivedUsersByRoleQuery("Admin"));
            return Ok(result);
        }

        [HttpPost("reset-password/{userId}")]
        public async Task<IActionResult> ResetPassword(
            string userId, [FromBody] ResetPasswordRequest request)
        {
            var result = await _mediator.Send(new ResetPasswordCommand(userId, request));
            return Ok(new { message = result });
        }
    }
}
```

### 8.6 Role-specific controller (Frontdesk example)

**`WebApi/Controllers/FrontDeskController.cs`**

```csharp
using Application.Services.User.Command;
using Application.Services.User.Query;
using Application.Services.User.Query.User;
using Application.Services.User.Request;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers
{
    [Route("api/frontdesk")]
    [ApiController]
    public class FrontDeskController : ControllerBase
    {
        private readonly IMediator _mediator;

        public FrontDeskController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("create-frontdesk")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateFrontDesk([FromBody] CreateFrontDeskRequest request)
        {
            var result = await _mediator.Send(new CreateFrontDeskCommand(request));
            return Ok(result);
        }

        [HttpPut("edit-frontdesk/{id}")]
        [Authorize(Roles = "Admin,Frontdesk")]
        public async Task<IActionResult> EditFrontDesk(string id, [FromBody] UpdateUserRequest request)
        {
            var result = await _mediator.Send(new UpdateUserCommand(id, request));
            return Ok(result);
        }

        [HttpDelete("delete-frontdesk/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFrontDesk(string id)
        {
            var result = await _mediator.Send(new DeleteUserCommand(id));
            return Ok(result);
        }

        [HttpGet("get-frontdesk/{id}")]
        [Authorize(Roles = "Admin,Frontdesk")]
        public async Task<IActionResult> GetFrontDeskById(string id)
        {
            var result = await _mediator.Send(new GetUserByIdQuery(id));
            return Ok(result);
        }

        [HttpGet("active-frontdesks")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllActiveFrontDesks()
        {
            // Generic query — just pass the role name
            var result = await _mediator.Send(new GetActiveUsersByRoleQuery("Frontdesk"));
            return Ok(result);
        }

        [HttpGet("archived-frontdesks")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAllArchivedFrontDesks()
        {
            var result = await _mediator.Send(new GetArchivedUsersByRoleQuery("Frontdesk"));
            return Ok(result);
        }
    }
}
```

### 8.7 Update validator (shared)

**`Application/Services/User/Validator/UpdateUserValidator.cs`**

```csharp
using Application.Services.User.Command;
using FluentValidation;

namespace Application.Services.User.Validator
{
    public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithMessage("User ID is required.");

            When(x => x.Request.FirstName != null, () =>
            {
                RuleFor(x => x.Request.FirstName)
                    .NotEmpty().MaximumLength(100);
            });

            When(x => x.Request.LastName != null, () =>
            {
                RuleFor(x => x.Request.LastName)
                    .NotEmpty().MaximumLength(100);
            });

            When(x => x.Request.Email != null, () =>
            {
                RuleFor(x => x.Request.Email)
                    .NotEmpty().EmailAddress();
            });

            When(x => x.Request.PhoneNumber != null, () =>
            {
                RuleFor(x => x.Request.PhoneNumber)
                    .Length(10).Matches("^[0-9]+$");
            });
        }
    }
}
```

---

## Phase 9 — Verification & Testing

### 9.1 Build

```bash
dotnet build
```

Must produce 0 errors.

### 9.2 Run

```bash
dotnet run --project WebApi
```

Check:
- Database is created with all tables
- Roles `Admin`, `Customer`, `Frontdesk` are seeded
- Default admin user exists

### 9.3 Auth flow test sequence

1. **Login:** `POST /api/auth/login` → get access token + refreshToken cookie
2. **Access protected endpoint:** `GET /api/admin/active-admins` with `Authorization: Bearer {token}`
3. **Refresh:** `POST /api/auth/refresh` with expired token in body → new tokens issued
4. **Change password:** `POST /api/auth/change-password` with Bearer token
5. **Logout:** `POST /api/auth/logout` → token invalidated, cookie cleared
6. **Breach detection:** Use the same refresh token twice → all user tokens invalidated

### 9.4 CRUD test sequence

1. **Create admin:** `POST /api/admin/create-admin`
2. **List active admins:** `GET /api/admin/active-admins`
3. **Get by ID:** `GET /api/admin/get-admin/{id}`
4. **Edit:** `PUT /api/admin/edit-admin/{id}`
5. **Delete (soft):** `DELETE /api/admin/delete-admin/{id}`
6. **List archived:** `GET /api/admin/archived-admins`
7. **Reset password:** `POST /api/admin/reset-password/{id}`

Repeat for Frontdesk endpoints.

### 9.5 Validation test

- Login with empty email → 400 with structured errors
- Create admin with 5-char password → 400 with specific message
- Create admin with duplicate email → 409

### 9.6 Error handling test

- Access protected endpoint without token → 401
- Access Admin endpoint with Frontdesk token → 403
- Get non-existent user → 404
- Delete already-deleted user → 409

---

## Quick Reference — Adding a New User Role

1. **Seed the role:** Add `"Farmer"` to the `roles` array in `DataSeeder.cs`
2. **Create files** (copy from CreateAdmin pattern):
   - `Application/Services/User/Request/CreateFarmerRequest.cs`
   - `Application/Services/User/Response/CreateFarmerResponse.cs`
   - `Application/Services/User/Command/CreateFarmerCommand.cs` — assign role `"Farmer"`
   - `Application/Services/User/Validator/CreateFarmerCommandValidator.cs`
3. **Create controller:** `WebApi/Controllers/FarmerController.cs`
   - `POST api/farmer/create-farmer` → `CreateFarmerCommand`
   - `PUT api/farmer/edit-farmer/{id}` → `UpdateUserCommand` (reuse)
   - `DELETE api/farmer/delete-farmer/{id}` → `DeleteUserCommand` (reuse)
   - `GET api/farmer/get-farmer/{id}` → `GetUserByIdQuery` (reuse)
   - `GET api/farmer/active-farmers` → `GetActiveUsersByRoleQuery("Farmer")` (reuse)
   - `GET api/farmer/archived-farmers` → `GetArchivedUsersByRoleQuery("Farmer")` (reuse)
4. **Build & test:** `dotnet build && dotnet run --project WebApi`

No changes needed to the generic queries, update command, delete command, or validation behavior — they all work for any role automatically.

---

## Key Architecture Decisions

| Decision | Rationale |
|----------|-----------|
| **Enums start at 1, not 0** | Distinguishes unset/default values from valid ones |
| **snake_case_lower JSON enums** | Frontend-friendly naming via `JsonStringEnumConverter(SnakeCaseLower)` |
| **snake_case DB columns** | PostgreSQL convention via `UseSnakeCaseNamingConvention()` |
| **Soft delete, not hard delete** | Preserves audit trail; `IsActive=false` + `LockoutEnd=MaxValue` |
| **Refresh token in HttpOnly cookie** | Prevents XSS from stealing tokens |
| **Rotating refresh tokens with breach detection** | If a used token is presented, ALL user tokens are invalidated |
| **`RequiresPasswordChange` flag** | Admin-created users must change password on first login |
| **Validators on Commands, not Requests** | Commands are the MediatR unit — validators fire in the pipeline automatically |
| **Role-specific create, generic everything else** | Minimizes code duplication while allowing role-specific creation logic |
| **`ValidationBehavior` throws `ValidationException`** | Caught by `GlobalExceptionHandler` → structured 400 response |
| **Logging redacts auth commands** | `LoginCommand`, `PasswordCommand`, `TokenCommand` bodies are `[REDACTED]` |
| **`.env` loaded via DotNetEnv** | Secrets never in `appsettings.json` or source control |
| **Auto-migrate on startup** | `dbContext.Database.MigrateAsync()` in `Program.cs` — safe for dev, review for prod |

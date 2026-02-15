using Application;
using Infrastructure;
using DotNetEnv;
using DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Persistence.Data;
using Microsoft.EntityFrameworkCore;

// Load environment variables from .env file
var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", ".env");
Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);
{
    // Add Persistence first (registers DbContext)
    builder.Services.AddPersistence(builder.Configuration);

    // Add Identity BEFORE Application (registers UserManager for MediatR handlers)
    builder.Services.AddIdentity<Domain.Entity.Identity.User, Domain.Entity.Identity.Role>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

    // Add services of the layers to the container
    builder.Services
        .AddWebApi()
        .AddApplication()
        .AddInfrastructure()
        .AddHealthChecks();
}

var app = builder.Build();

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
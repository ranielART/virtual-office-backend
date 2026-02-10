using Application;
using Infrastructure;
using DotNetEnv;
using DependencyInjection;

// Load environment variables from .env file
var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", ".env");
Env.Load(envPath);

var builder = WebApplication.CreateBuilder(args);
{
    // Add services of the layers to the container.
    builder.Services.
        AddWebApi().
        AddApplication().
        AddInfrastructure().
        AddPersistence(builder.Configuration).
        AddHealthChecks();
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
using System.Text.Json;
using System.Text.Json.Serialization;
using CashCoach.Api.Errors;
using CashCoach.Api.Users;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Api.Configuration;

public static class ApiSetup
{
    private const string DefaultFrontendOrigin = "http://localhost:5173";

    /// <summary>JSON, CORS, error handling, OpenAPI and per-request user resolution.</summary>
    public static IServiceCollection AddCashCoachApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() is { Length: > 0 } origins
            ? origins
            : [DefaultFrontendOrigin];
        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

        // Without this, minimal-API binding failures return an empty 400 instead of reaching ApiExceptionHandler.
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();

        services.AddOpenApi();
        services.AddScoped<CurrentUser>();

        return services;
    }

    /// <summary>SQLite <see cref="AppDbContext"/> from <c>ConnectionStrings:Default</c>; relative file paths are anchored to the content root.</summary>
    public static IServiceCollection AddCashCoachDatabase(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("Default")
                ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");
            var contentRoot = serviceProvider.GetRequiredService<IHostEnvironment>().ContentRootPath;

            options.UseSqlite(SqliteConnectionStrings.Resolve(connectionString, contentRoot));
        });

        return services;
    }

    public static async Task EnsureDatabaseCreatedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
    }

    public static WebApplication UseCashCoachApi(this WebApplication app)
    {
        app.UseCors();
        app.UseExceptionHandler();
        return app;
    }
}

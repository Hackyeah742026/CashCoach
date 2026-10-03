using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CashCoach.Api.Contracts;
using CashCoach.Api.Errors;
using CashCoach.Api.Users;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Api.Configuration;

public static class ApiSetup
{
    private const string DefaultFrontendOrigin = "http://localhost:5173";
    public const int ChatRequestsPerMinute = 20;

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

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Endpoints.ChatEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Request.Headers[CurrentUserFilter.HeaderName].ToString(),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = ChatRequestsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse(new ErrorDetail("rate_limited", $"At most {ChatRequestsPerMinute} chat requests per minute.")), cancellationToken);
            };
        });

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

    /// <summary>
    /// Creates the schema. There are no migrations: a database from an older schema version (SQLite <c>user_version</c>)
    /// only holds demo data, so it is dropped and recreated.
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;
        if (!await database.EnsureCreatedAsync() && await ReadSchemaVersionAsync(database) != AppDbContext.SchemaVersion)
        {
            await database.EnsureDeletedAsync();
            await database.EnsureCreatedAsync();
        }

        await database.ExecuteSqlRawAsync($"PRAGMA user_version = {AppDbContext.SchemaVersion}");
    }

    private static async Task<int> ReadSchemaVersionAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade database) =>
        await database.SqlQueryRaw<int>("SELECT user_version AS Value FROM pragma_user_version").SingleAsync();

    public static WebApplication UseCashCoachApi(this WebApplication app)
    {
        app.UseCors();
        app.UseExceptionHandler();
        app.UseRateLimiter();
        return app;
    }
}

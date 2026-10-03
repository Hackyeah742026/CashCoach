using CashCoach.Api.Configuration;
using CashCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CashCoach.Api.Tests;

/// <summary>
/// A host with the production API services and middleware but test-only endpoints under <c>/api</c>,
/// for exercising cross-cutting behavior (error format, user resolution) before business endpoints exist.
/// </summary>
public sealed class TestApiHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly SqliteConnection _connection;

    private TestApiHost(WebApplication app, SqliteConnection connection)
    {
        _app = app;
        _connection = connection;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public static async Task<TestApiHost> StartAsync(Action<RouteGroupBuilder> mapEndpoints)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Services.AddCashCoachApi(builder.Configuration);
        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        var app = builder.Build();
        await app.EnsureDatabaseCreatedAsync();
        app.UseCashCoachApi();
        mapEndpoints(app.MapGroup("/api"));
        await app.StartAsync();

        return new TestApiHost(app, connection);
    }

    public async Task SeedAsync(params object[] entities)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
        await _connection.DisposeAsync();
    }
}

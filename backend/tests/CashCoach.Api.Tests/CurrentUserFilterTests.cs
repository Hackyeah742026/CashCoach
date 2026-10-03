using System.Net;
using CashCoach.Api.Users;
using CashCoach.Core.Domain;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CashCoach.Api.Tests;

public class CurrentUserFilterTests
{
    private static Task<TestApiHost> StartHostAsync() => TestApiHost.StartAsync(api =>
        api.MapGroup("").RequireCurrentUser()
            .MapGet("/whoami", (CurrentUser currentUser) => TypedResults.Ok(currentUser.User.Name)));

    private static HttpRequestMessage WhoAmI(string? userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/whoami");
        if (userId is not null)
        {
            request.Headers.Add("X-User-Id", userId);
        }

        return request;
    }

    [Fact]
    public async Task Missing_header_returns_401_missing_user_id()
    {
        await using var host = await StartHostAsync();

        var response = await host.Client.SendAsync(WhoAmI(null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().Contain("""{"error":{"code":"missing_user_id",""");
    }

    [Fact]
    public async Task Malformed_header_returns_400_invalid_user_id()
    {
        await using var host = await StartHostAsync();

        var response = await host.Client.SendAsync(WhoAmI("not-a-guid"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("""{"error":{"code":"invalid_user_id",""");
    }

    [Fact]
    public async Task Unknown_user_returns_404_user_not_found()
    {
        await using var host = await StartHostAsync();

        var response = await host.Client.SendAsync(WhoAmI(Guid.NewGuid().ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("""{"error":{"code":"user_not_found",""");
    }

    [Fact]
    public async Task Known_user_is_resolved_into_CurrentUser()
    {
        await using var host = await StartHostAsync();
        var user = new User { Id = Guid.NewGuid(), Name = "Ola", Persona = Persona.Student, CreatedAt = DateTime.UtcNow };
        await host.SeedAsync(user);

        var response = await host.Client.SendAsync(WhoAmI(user.Id.ToString()));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("\"Ola\"");
    }
}

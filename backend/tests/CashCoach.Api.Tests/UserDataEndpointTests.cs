using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Api.Tests;

public class UserDataEndpointTests
{
    [Fact]
    public async Task Export_returns_everything_stored_about_the_user_as_a_download()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var response = await client.GetAsync("/api/me/export");
        using var export = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        export.RootElement.GetProperty("user").GetProperty("name").GetString().Should().Be("Ola");
        export.RootElement.GetProperty("transactions").GetArrayLength().Should().BeGreaterThan(100);
        export.RootElement.GetProperty("recurring_groups").GetArrayLength().Should().BePositive();
    }

    [Fact]
    public async Task Delete_me_removes_the_user_and_all_their_data()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        (await client.DeleteAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/me")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await factory.WithDbAsync(db => db.Transactions.CountAsync())).Should().Be(0);
        (await factory.WithDbAsync(db => db.RecurringGroups.CountAsync())).Should().Be(0);
    }
}

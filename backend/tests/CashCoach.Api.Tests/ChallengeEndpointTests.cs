using System.Net;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Analytics;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class ChallengeEndpointTests
{
    [Fact]
    public async Task Check_in_grows_the_streak_once_per_day()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var challenge = await (await client.PostAsJsonAsync("/api/challenges", new { type = "no_delivery", days = 5 })).ReadAsync<ChallengeDto>();
        var checkIn = await (await client.PostAsync($"/api/challenges/{challenge.Id}/checkin", null)).ReadAsync<CheckInResponse>();
        var again = await client.PostAsync($"/api/challenges/{challenge.Id}/checkin", null);

        challenge.Title.Should().Be("5 dni bez jedzenia z dostawą");
        checkIn.Broken.Should().BeFalse();
        checkIn.Challenge.Streak.Should().Be(1);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.GetFromJsonAsync<List<ChallengeDto>>("/api/challenges", ApiTestClient.Json)).Should().ContainSingle();
    }

    [Fact]
    public async Task A_delivery_payment_after_the_start_breaks_the_streak_and_raises_an_alert()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        var challenge = await (await client.PostAsJsonAsync("/api/challenges", new { type = "no_delivery" })).ReadAsync<ChallengeDto>();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        (await client.UploadCsvAsync($"date;amount;description;currency\n{today:yyyy-MM-dd};-45,90;GLOVO*ORDER 999 KRAKOW;PLN\n")).EnsureSuccessStatusCode();
        var alerts = await (await client.GetAsync("/api/alerts")).ReadAsync<List<AlertDto>>();
        var checkIn = await (await client.PostAsync($"/api/challenges/{challenge.Id}/checkin", null)).ReadAsync<CheckInResponse>();

        alerts.Should().Contain(a => a.Type == AlertType.Challenge);
        checkIn.Broken.Should().BeTrue();
        checkIn.BreakDate.Should().Be(today);
        checkIn.Challenge.Streak.Should().Be(0);
        checkIn.Evidence.TransactionIds.Should().ContainSingle();
    }

    [Fact]
    public async Task Unknown_challenge_types_are_rejected()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        (await client.PostAsJsonAsync("/api/challenges", new { type = "no_coffee" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/challenges", new { type = "no_taxi", days = 0 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync($"/api/challenges/{Guid.NewGuid()}/checkin", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

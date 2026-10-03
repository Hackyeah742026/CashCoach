using System.Net;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Analytics;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class GoalEndpointTests
{
    [Fact]
    public async Task Goals_can_be_created_updated_deposited_into_and_deleted()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var created = await client.PostAsJsonAsync("/api/goals", new { name = "Koncert", emoji = "🎸", target = 1200m, saved = 200m, deadline = "2026-12-30" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var goal = await created.ReadAsync<GoalDto>();

        goal.Remaining.Should().Be(1000m);
        goal.MonthlyPlan.Should().Be(333.34m, "without a plan the goal plans what the deadline requires (1000 zł in 3 monthly deposits)");
        goal.Status.Should().Be(GoalStatus.OnTrack);
        goal.RequiredPerWeek.Should().BePositive();

        var patched = await (await client.PatchJsonAsync($"/api/goals/{goal.Id}", new { monthly_plan = 100m })).ReadAsync<GoalDto>();
        patched.Status.Should().Be(GoalStatus.Behind);
        patched.ShortBy.Should().Be(1000m - 3 * 100m);

        var deposited = await (await client.PostAsJsonAsync($"/api/goals/{goal.Id}/deposit", new { amount = 1000m })).ReadAsync<GoalDto>();
        deposited.Status.Should().Be(GoalStatus.Done);
        deposited.Saved.Should().Be(1200m);

        (await client.GetFromJsonAsync<List<GoalDto>>("/api/goals", ApiTestClient.Json)).Should().ContainSingle();
        (await client.DeleteAsync($"/api/goals/{goal.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/goals/{goal.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Invalid_goals_are_rejected()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        (await client.PostAsJsonAsync("/api/goals", new { name = "", target = 100m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/goals", new { name = "X", target = 0m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/goals", new { name = "X", target = 10m, deadline = "2026-01-01" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync($"/api/goals/{Guid.NewGuid()}/deposit", new { amount = 5m })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_gives_a_verdict_and_a_savings_plan()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        var preview = await (await client.PostAsJsonAsync("/api/goals/preview", new { target = 12000m, deadline = "2027-03-30" })).ReadAsync<GoalPreviewResponse>();

        preview.RequiredPerMonth.Should().Be(2000m, "12000 zł in 6 monthly deposits");
        preview.Verdict.Should().NotBe(Verdict.Green, "2000 zł a month is more than the persona saves");
        preview.Plan.Should().NotBeEmpty();
    }
}

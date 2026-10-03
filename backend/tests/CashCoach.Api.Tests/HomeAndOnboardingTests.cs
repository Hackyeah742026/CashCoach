using System.Net;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class HomeAndOnboardingTests
{
    [Fact]
    public async Task A_new_user_sets_assumptions_and_then_loads_demo_data()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/users", new { language = "en" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await created.ReadAsync<UserProfileResponse>();
        client.DefaultRequestHeaders.Add("X-User-Id", user.UserId.ToString());

        user.Persona.Should().Be(Persona.Custom);
        user.HasData.Should().BeFalse();
        user.Language.Should().Be("en");

        await client.PatchJsonAsync("/api/me", new { payday = 10, balance = 1500m });
        var import = await (await client.PostAsJsonAsync("/api/import/demo", new { persona = "first_job" })).ReadAsync<ImportResponse>();
        var me = await (await client.GetAsync("/api/me")).ReadAsync<UserProfileResponse>();

        import.Imported.Should().BeInRange(100, 250);
        me.HasData.Should().BeTrue();
        me.Payday.Should().Be(28, "the demo salary decides the payday");
        me.Balance.Should().Be(1500m, "a balance the user set is kept");
        (await client.PostAsJsonAsync("/api/import/demo", new { persona = "custom" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.CreateClient().PostAsJsonAsync("/api/demo/login", new { persona = "custom" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Home_returns_the_month_summary_with_a_template_narrative()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        var home = await (await client.GetAsync("/api/home")).ReadAsync<HomeResponse>();
        var july = await (await client.GetAsync("/api/home?month=2026-07")).ReadAsync<HomeResponse>();

        home.Month.Should().Be("2026-09");
        home.Expenses.Should().BeNegative();
        home.Saved.Should().Be(home.Income + home.Expenses);
        home.Status.Should().Be(ForecastStatus.Danger);
        home.PayPeriod.Should().Be(new PayPeriodDto(new DateOnly(2026, 9, 15), new DateOnly(2026, 10, 15), 15));
        home.SafeToSpendEvidence.Calculation.Should().Contain(" − ");
        home.ByCategory.Should().BeInAscendingOrder(c => c.Amount);
        home.ByCategory.Sum(c => c.Amount).Should().Be(home.Expenses);
        home.Recurring.Should().Contain(r => r.Merchant == "Spotify" && r.Amount == -23.99m);
        home.Narrative.AiGenerated.Should().BeFalse();
        home.Narrative.FactCheck.Should().Be("fallback");
        home.Narrative.Bullets.Should().Contain(b => b.Text.Contains("skończą się"));
        july.Narrative.Bullets.Should().NotContain(b => b.FactKeys.Contains("forecast.safe_to_spend"), "the forecast belongs to the current month only");
        (await client.GetAsync("/api/home?month=2025-01")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Transactions_by_id_include_their_sum_and_wrapped_has_story_captions()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");
        var page = await (await client.GetAsync("/api/transactions?limit=2")).ReadAsync<TransactionListResponse>();

        var byIds = await (await client.GetAsync($"/api/transactions?ids={page.Items[0].Id},{page.Items[1].Id}")).ReadAsync<TransactionListResponse>();
        var wrapped = await (await client.GetAsync("/api/wrapped?month=2026-09")).ReadAsync<WrappedResponse>();
        var preview = await (await client.PostAsJsonAsync("/api/goals/preview", new { target = 500m, deadline = "2027-03-30" })).ReadAsync<GoalPreviewResponse>();

        byIds.Sum.Should().Be(page.Items[0].Amount + page.Items[1].Amount);
        wrapped.Captions.TotalSpent.Should().NotBeEmpty();
        wrapped.Captions.BiggestChange.Should().NotBeEmpty();
        preview.Text.Should().NotBeEmpty();
    }
}

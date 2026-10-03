using System.Net;
using System.Net.Http.Json;
using CashCoach.Api.Contracts;
using CashCoach.Core.Analytics;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class AnalyticsEndpointTests
{
    [Fact]
    public async Task Bnpl_persona_runs_out_of_money_before_payday()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        var forecast = await (await client.GetAsync("/api/forecast")).ReadAsync<ForecastResponse>();

        forecast.Status.Should().Be(ForecastStatus.Danger);
        forecast.RunOutDate.Should().NotBeNull().And.BeBefore(forecast.NextPayday);
        forecast.AsOf.Should().Be(new DateOnly(2026, 9, 30));
        forecast.NextPayday.Should().Be(new DateOnly(2026, 10, 15));
        forecast.Series.Should().HaveCount(forecast.DaysLeft + 1);
        forecast.Evidence.Figures.Should().Contain(f => f.Key == "forecast.safe_to_spend" && f.Amount == forecast.SafeToSpend);

        var ids = string.Join(',', forecast.Evidence.TransactionIds.Take(3));
        var evidence = await (await client.GetAsync($"/api/transactions?ids={ids}")).ReadAsync<TransactionListResponse>();
        evidence.Items.Select(t => t.Id).Should().BeEquivalentTo(forecast.Evidence.TransactionIds.Take(3));
        (await client.GetAsync("/api/transactions?ids=nope")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("student")]
    [InlineData("first_job")]
    [InlineData("bnpl_heavy")]
    public async Task Every_persona_gets_at_least_three_opportunities_with_amounts(string persona)
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync(persona);

        var opportunities = await (await client.GetAsync("/api/opportunities")).ReadAsync<OpportunitiesResponse>();

        opportunities.Items.Should().HaveCountGreaterThanOrEqualTo(3).And.HaveCountLessThanOrEqualTo(5);
        opportunities.Items.Should().OnlyContain(o => o.MonthlySaving > 0 && o.YearlySaving == o.MonthlySaving * 12 && o.Title.Length > 0);
        opportunities.Items.Should().OnlyContain(o => !o.AiGenerated, "the fake LLM is unavailable, so templates are used");
        opportunities.TotalMonthlySaving.Should().Be(opportunities.Items.Sum(o => o.MonthlySaving));
    }

    [Fact]
    public async Task Dismissed_opportunities_and_alerts_are_hidden()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");
        var before = await (await client.GetAsync("/api/opportunities")).ReadAsync<OpportunitiesResponse>();
        var alerts = await (await client.GetAsync("/api/alerts")).ReadAsync<List<AlertDto>>();

        (await client.PostAsync($"/api/opportunities/{before.Items[0].Id}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync($"/api/alerts/{Uri.EscapeDataString(alerts[0].Id)}/dismiss", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await (await client.GetAsync("/api/opportunities")).ReadAsync<OpportunitiesResponse>();
        after.Items.Should().NotContain(o => o.Id == before.Items[0].Id);
        (await (await client.GetAsync("/api/alerts")).ReadAsync<List<AlertDto>>()).Should().NotContain(a => a.Id == alerts[0].Id);
    }

    [Fact]
    public async Task Bnpl_persona_gets_run_out_bnpl_and_duplicate_alerts()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        var alerts = await (await client.GetAsync("/api/alerts")).ReadAsync<List<AlertDto>>();

        alerts.Select(a => a.Type).Should().Contain([AlertType.RunOut, AlertType.Bnpl, AlertType.DuplicateSub]);
        alerts[0].Type.Should().Be(AlertType.RunOut);
        alerts.Should().OnlyContain(a => a.Title.Length > 0 && a.Message.Length > 0);
    }

    [Theory]
    [InlineData("student")]
    [InlineData("first_job")]
    [InlineData("bnpl_heavy")]
    public async Task Wrapped_has_eight_cards_for_every_persona(string persona)
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync(persona);

        var months = await (await client.GetAsync("/api/wrapped/months")).ReadAsync<List<WrappedMonthDto>>();
        var wrapped = await (await client.GetAsync("/api/wrapped?month=2026-09")).ReadAsync<WrappedResponse>();

        months.Select(m => m.Month).Should().Equal("2026-09", "2026-08", "2026-07");
        months[0].IsNew.Should().BeTrue();
        wrapped.Cards.Select(c => c.Type).Should().Equal(WrappedCardTypes.All);
        wrapped.Cards.Should().OnlyContain(c => c.Caption.Length > 0 && !c.AiGenerated);
        wrapped.TotalSpent.Should().BePositive();
        wrapped.Personality.Title.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Wrapped_rejects_bad_and_empty_months()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        (await client.GetAsync("/api/wrapped?month=09-2026")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/wrapped?month=2025-01")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Dashboard_composes_forecast_month_opportunities_goals_alerts_and_subscriptions()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("first_job");
        await client.PostAsJsonAsync("/api/goals", new { name = "Wakacje", emoji = "🏖️", target = 3000m, deadline = "2027-06-30" });

        var dashboard = await (await client.GetAsync("/api/dashboard")).ReadAsync<DashboardResponse>();

        dashboard.User.Name.Should().Be("Kuba");
        dashboard.AsOf.Should().Be(new DateOnly(2026, 9, 30));
        dashboard.Forecast.Status.Should().Be(ForecastStatus.Ok);
        dashboard.Month.Month.Should().Be("2026-09");
        dashboard.Month.Expenses.Should().BeNegative();
        dashboard.Month.Saved.Should().Be(dashboard.Month.Income + dashboard.Month.Expenses);
        dashboard.Opportunities.Should().HaveCount(3);
        dashboard.Goals.Should().ContainSingle(g => g.Name == "Wakacje");
        dashboard.Subscriptions.Duplicates.Should().Be(2);
        dashboard.Bnpl.ActivePlans.Should().Be(1);
    }

    [Fact]
    public async Task Purchase_and_change_simulations()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("first_job");

        var cheap = await (await client.PostAsJsonAsync("/api/simulate/purchase", new { amount = 100m, item = "Bilety" })).ReadAsync<SimulatePurchaseResponse>();
        var expensive = await (await client.PostAsJsonAsync("/api/simulate/purchase", new { amount = 9000m })).ReadAsync<SimulatePurchaseResponse>();
        var change = await (await client.PostAsJsonAsync("/api/simulate/change", new { category = "food_delivery", new_per_week = 50m })).ReadAsync<SimulateChangeResponse>();

        cheap.Verdict.Should().Be(Verdict.Green);
        cheap.LeftAfter.Should().Be(cheap.SafeToSpend - 100m);
        cheap.Item.Should().Be("Bilety");
        cheap.Explanation.Should().NotBeEmpty();
        expensive.Verdict.Should().Be(Verdict.Red);
        expensive.Shortfall.Should().Be(9000m - expensive.SafeToSpend);
        expensive.After.ProjectedEnd.Should().Be(expensive.Before.ProjectedEnd - 9000m);
        change.MonthlySaving.Should().BePositive();
        change.YearlySaving.Should().Be(change.MonthlySaving * 12);

        var whatIf = await (await client.PostAsJsonAsync("/api/simulate/purchase", new { amount = 100m, assumptions = new { balance = 10_000m } })).ReadAsync<SimulatePurchaseResponse>();
        whatIf.Assumptions.Balance.Should().Be(10_000m);
        whatIf.SafeToSpend.Should().Be(cheap.SafeToSpend + 10_000m - cheap.Assumptions.Balance);
        (await client.GetAsync("/api/me")).ReadAsync<UserProfileResponse>().Result.Balance.Should().Be(cheap.Assumptions.Balance, "what-if assumptions are not saved");

        (await client.PostAsJsonAsync("/api/simulate/purchase", new { amount = -5m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsJsonAsync("/api/simulate/change", new { category = "pizza", new_per_week = 5m })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

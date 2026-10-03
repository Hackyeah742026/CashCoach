using System.Net;
using CashCoach.Api.Contracts;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Api.Tests;

public class InsightEndpointTests
{
    [Fact]
    public async Task Summary_breaks_down_spending_for_the_latest_month()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var summary = await (await client.GetAsync("/api/summary?period=this_month")).ReadAsync<SummaryResponse>();
        var quarter = await (await client.GetAsync("/api/summary?period=last_3_months")).ReadAsync<SummaryResponse>();

        summary.Period.Should().Be(new PeriodDto(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
        summary.TotalSpent.Should().Be(summary.Categories.Sum(c => c.Amount));
        summary.Categories.Should().Contain(c => c.Category == Category.Rent && c.Amount == 1100.00m);
        summary.Categories.Should().BeInDescendingOrder(c => c.Amount);
        summary.Categories.Sum(c => c.Share).Should().BeApproximately(1m, 0.001m);
        summary.Categories.Should().OnlyContain(c => c.TopMerchants.Count >= 1 && c.TopMerchants.Count <= 3);
        summary.Categories.Single(c => c.Category == Category.Rent).VsPrevPct.Should().Be(0m);
        quarter.Period.Should().Be(new PeriodDto(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30)));
        quarter.TotalSpent.Should().BeGreaterThan(summary.TotalSpent);
        (await client.GetAsync("/api/summary?period=forever")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Subscriptions_list_planted_services_flags_the_music_duplicate_and_records_still_using()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("student");

        var subscriptions = await (await client.GetAsync("/api/subscriptions")).ReadAsync<SubscriptionsResponse>();

        subscriptions.Items.Select(s => s.Merchant).Should().BeEquivalentTo("Spotify", "YouTube Music", "Netflix", "iCloud+");
        subscriptions.Items.Where(s => s.Duplicate).Select(s => s.Merchant).Should().BeEquivalentTo("Spotify", "YouTube Music");
        subscriptions.Items.Single(s => s.Merchant == "Spotify").Should().Match<SubscriptionDto>(s =>
            s.Group == "music" && s.Amount == 23.99m && s.NextDate == new DateOnly(2026, 10, 6) && s.UserConfirmed == null);
        subscriptions.MonthlyTotal.Should().Be(23.99m + 23.99m + 33.00m + 3.99m);

        var spotify = subscriptions.Items.Single(s => s.Merchant == "Spotify");
        var patched = await (await client.PatchJsonAsync($"/api/subscriptions/{spotify.Id}", new { still_using = false })).ReadAsync<UpdateSubscriptionResponse>();
        var after = await (await client.GetAsync("/api/subscriptions")).ReadAsync<SubscriptionsResponse>();

        var opportunities = await (await client.GetAsync("/api/opportunities")).ReadAsync<OpportunitiesResponse>();

        patched.Should().Be(new UpdateSubscriptionResponse(spotify.Id, false, $"unused_subscription:{spotify.Id}"));
        after.Items.Single(s => s.Id == spotify.Id).UserConfirmed.Should().BeFalse();
        opportunities.Items.Should().Contain(o => o.Id == patched.NewOpportunityId && o.MonthlySaving == 23.99m);
        (await client.PatchJsonAsync($"/api/subscriptions/{Guid.NewGuid()}", new { still_using = true })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Bnpl_lists_active_plans_with_remaining_amounts_and_an_explainer()
    {
        await using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("bnpl_heavy");

        var bnpl = await (await client.GetAsync("/api/bnpl")).ReadAsync<BnplResponse>();

        bnpl.ActivePlans.Should().Be(3);
        bnpl.Items.Should().BeEquivalentTo(
        [
            new BnplPlanDto("Klarna", "Zalando", 99.99m, 4, 6, 199.98m, new DateOnly(2026, 10, 7)),
            new BnplPlanDto("PayPo", "Modivo", 149.00m, 2, 3, 149.00m, new DateOnly(2026, 10, 20)),
            new BnplPlanDto("Twisto", "Media Expert", 333.00m, 1, 6, 1665.00m, new DateOnly(2026, 10, 12)),
        ]);
        bnpl.TotalRemaining.Should().Be(199.98m + 149.00m + 1665.00m);
        bnpl.Explainer.Should().Contain("3 aktywne plany").And.Contain("2");
    }
}

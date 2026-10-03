using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class OpportunityFinderTests
{
    [Fact]
    public void Food_delivery_saving_is_half_of_the_monthly_spend()
    {
        var builder = new SnapshotBuilder();
        builder.Daily("2026-09-01", "2026-09-30", 1m, "Żabka");
        for (var day = 1; day <= 30; day += 3)
        {
            builder.Spend($"2026-09-{day:00}", 40m, "Glovo", Category.FoodDelivery);
        }

        var delivery = OpportunityFinder.Find(builder.Build()).Single(o => o.Type == OpportunityType.FoodDelivery);

        delivery.MonthlySpendGr.Should().Be(40_000);
        delivery.MonthlySavingGr.Should().Be(20_000);
        delivery.YearlySavingGr.Should().Be(240_000);
        delivery.Count.Should().Be(10);
        delivery.TransactionIds.Should().HaveCount(10);
        delivery.TitleKey.Should().Be("opportunity.food_delivery");
    }

    [Fact]
    public void Duplicate_subscriptions_save_all_but_the_most_expensive_and_unused_ones_are_separate()
    {
        var builder = new SnapshotBuilder();
        builder.Spend("2026-09-01", 1m);
        builder.Recurring("Spotify", RecurringType.Subscription, 23.99m, "2026-10-06");
        builder.Recurring("YouTube Music", RecurringType.Subscription, 19.99m, "2026-10-14");
        builder.Recurring("Tidal", RecurringType.Subscription, 29.99m, "2026-10-20", stillUsing: false);
        builder.Recurring("Netflix", RecurringType.Subscription, 49m, "2026-10-20");

        var opportunities = OpportunityFinder.Find(builder.Build());

        opportunities.Should().ContainSingle(o => o.Id == "duplicate_subscription:music").Which.MonthlySavingGr.Should().Be(1_999);
        opportunities.Should().ContainSingle(o => o.Type == OpportunityType.UnusedSubscription).Which.MonthlySavingGr.Should().Be(2_999);
        opportunities.Should().NotContain(o => o.Id == "duplicate_subscription:video");
    }

    [Fact]
    public void Taxi_small_buys_and_bnpl_are_found_and_sorted_by_saving()
    {
        var builder = new SnapshotBuilder();
        builder.Daily("2026-09-01", "2026-09-30", 12m, "Żabka");
        for (var day = 1; day <= 28; day += 4)
        {
            builder.Spend($"2026-09-{day:00}", 30m, "Bolt", Category.Transport);
        }

        builder.Expense("2026-08-12", 333m, "Media Expert", Category.Bnpl, bnpl: true).RawDescription = "TWISTO*MEDIA EXPERT RATA 1/6";
        builder.Expense("2026-09-12", 333m, "Media Expert", Category.Bnpl, bnpl: true).RawDescription = "TWISTO*MEDIA EXPERT RATA 2/6";

        var opportunities = OpportunityFinder.Find(builder.Build());

        opportunities.Select(o => o.Type).Should().Equal(OpportunityType.Bnpl, OpportunityType.SmallDailyBuys, OpportunityType.TaxiRides);
        opportunities.Should().BeInDescendingOrder(o => o.MonthlySavingGr);
        // The history starts on Aug 12, so the window is 50 days.
        opportunities.Single(o => o.Type == OpportunityType.TaxiRides).MonthlySavingGr.Should().Be(Money.Round(7 * 3_000 * 30m / 50 / 2));
    }

    [Fact]
    public void Nothing_is_suggested_for_modest_spending()
    {
        var builder = new SnapshotBuilder();
        builder.Spend("2026-09-01", 50m, "Glovo", Category.FoodDelivery);
        builder.Spend("2026-09-30", 50m, "Lidl", Category.Groceries);

        OpportunityFinder.Find(builder.Build()).Should().BeEmpty();
    }
}

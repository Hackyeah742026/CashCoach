using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class InsightCalculatorTests
{
    private static Transaction Tx(string date, long amountGr, string merchant, Category category) => new()
    {
        Id = Guid.NewGuid(),
        Date = DateOnly.Parse(date),
        AmountGr = amountGr,
        Merchant = merchant,
        Category = category,
    };

    [Theory]
    [InlineData("this_month", "2026-09-01", "2026-09-30", "2026-08-01", "2026-08-31")]
    [InlineData("last_month", "2026-08-01", "2026-08-31", "2026-07-01", "2026-07-31")]
    [InlineData("last_3_months", "2026-07-01", "2026-09-30", "2026-04-01", "2026-06-30")]
    public void Periods_are_calendar_months_relative_to_the_reference_date(string value, string from, string to, string prevFrom, string prevTo)
    {
        SummaryPeriods.TryParse(value, out var period).Should().BeTrue();

        var (current, previous) = SummaryPeriods.Resolve(period, new DateOnly(2026, 9, 17));

        current.Should().Be(new DateRange(DateOnly.Parse(from), DateOnly.Parse(to)));
        previous.Should().Be(new DateRange(DateOnly.Parse(prevFrom), DateOnly.Parse(prevTo)));
    }

    [Fact]
    public void Summary_totals_expenses_by_category_with_share_change_and_top_merchants()
    {
        var transactions = new[]
        {
            Tx("2026-09-02", -6_000, "Glovo", Category.FoodDelivery),
            Tx("2026-09-03", -4_000, "Pyszne.pl", Category.FoodDelivery),
            Tx("2026-09-04", -2_000, "Glovo", Category.FoodDelivery),
            Tx("2026-09-05", -8_000, "Biedronka", Category.Groceries),
            Tx("2026-09-10", 500_000, "Wynagrodzenie", Category.Salary),
            Tx("2026-08-05", -8_000, "Glovo", Category.FoodDelivery),
        };

        var summary = SpendingSummaryCalculator.Summarize(transactions, SummaryPeriod.ThisMonth, new DateOnly(2026, 9, 30));

        summary.TotalSpentGr.Should().Be(20_000);
        summary.Categories.Should().HaveCount(2);
        var food = summary.Categories[0];
        food.Category.Should().Be(Category.FoodDelivery);
        food.AmountGr.Should().Be(12_000);
        food.Count.Should().Be(3);
        food.Share.Should().Be(0.6m);
        food.VsPrevPct.Should().Be(50.0m);
        food.TopMerchants.Should().Equal(new MerchantSpending("Glovo", 8_000, 2), new MerchantSpending("Pyszne.pl", 4_000, 1));
        summary.Categories[1].VsPrevPct.Should().BeNull();
    }

    [Fact]
    public void Subscriptions_in_the_same_group_are_flagged_as_duplicates()
    {
        RecurringGroup Sub(string merchant, long amountGr, int periodDays = 30) =>
            new() { Id = Guid.NewGuid(), Merchant = merchant, Type = RecurringType.Subscription, AvgAmountGr = amountGr, PeriodDays = periodDays };

        var overview = SubscriptionOverviewCalculator.Build(
        [
            Sub("Spotify", -2_399),
            Sub("YouTube Music", -2_399),
            Sub("Netflix", -3_300),
            Sub("iCloud+", -399),
            Sub("Gazeta", -1_200, periodDays: 7),
        ]);

        overview.Items.Where(i => i.Duplicate).Select(i => i.Subscription.Merchant).Should().BeEquivalentTo("Spotify", "YouTube Music");
        overview.Items.Single(i => i.Subscription.Merchant == "Netflix").Should().Match<SubscriptionItem>(i => i.Group == "video" && !i.Duplicate);
        overview.Items.Single(i => i.Subscription.Merchant == "iCloud+").Group.Should().BeNull();
        overview.MonthlyTotalGr.Should().Be(2_399 + 2_399 + 3_300 + 399 + 5_200);
    }

    [Fact]
    public void Money_to_zloty_keeps_two_decimals()
    {
        Money.ToZloty(-110_000).ToString(System.Globalization.CultureInfo.InvariantCulture).Should().Be("-1100.00");
        Money.ToZloty(2_399).Should().Be(23.99m);
    }
}

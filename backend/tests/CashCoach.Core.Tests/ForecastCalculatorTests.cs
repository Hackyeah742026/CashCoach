using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class ForecastCalculatorTests
{
    [Theory]
    [InlineData("2026-09-30", 10, "2026-10-10")]
    [InlineData("2026-10-09", 10, "2026-10-10")]
    [InlineData("2026-10-10", 10, "2026-11-10")]
    [InlineData("2026-01-31", 31, "2026-02-28")]
    [InlineData("2026-09-15", null, "2026-10-01")]
    public void Next_payday_is_strictly_after_today_and_clamped_to_the_month(string today, int? payday, string expected) =>
        ForecastCalculator.NextPayday(DateOnly.Parse(today), payday).Should().Be(DateOnly.Parse(expected));

    [Fact]
    public void Upcoming_payments_include_bills_before_payday_and_only_the_next_bnpl_instalment()
    {
        var builder = new SnapshotBuilder { Payday = 15 };
        builder.Recurring("Czynsz", RecurringType.Rent, 1600m, "2026-10-01");
        builder.Recurring("Spotify", RecurringType.Subscription, 23.99m, "2026-10-06");
        builder.Recurring("Disney+", RecurringType.Subscription, 37.99m, "2026-10-22");
        builder.Recurring("Zalando", RecurringType.Bnpl, 99.99m, "2026-10-07");
        builder.Recurring("Gym", RecurringType.Subscription, 20m, "2026-09-02", periodDays: 7);

        var forecast = ForecastCalculator.Compute(builder.Build());

        forecast.Upcoming.Select(p => (p.Merchant, p.Date.Day)).Should().Equal(
            ("Czynsz", 1), ("Spotify", 6), ("Gym", 7), ("Zalando", 7), ("Gym", 14));
        forecast.FixedUpcomingGr.Should().Be(160_000 + 2_399 + 9_999 + 2 * 2_000);
    }

    [Fact]
    public void Daily_variable_is_the_median_of_the_last_30_days_without_recurring_payments()
    {
        var builder = new SnapshotBuilder();
        builder.Daily("2026-09-01", "2026-09-30", 20m);
        builder.Spend("2026-09-10", 500m, "Allegro", Category.Shopping);
        builder.Expense("2026-09-01", 1600m, "Czynsz", Category.Rent, recurring: true);

        ForecastCalculator.DailyVariable(builder.Build()).Should().Be(2_000);
    }

    [Fact]
    public void Projection_is_balance_minus_bills_minus_daily_spending_times_days_left()
    {
        var builder = new SnapshotBuilder { Balance = 1000m, Payday = 10, Buffer = 300m };
        builder.Daily("2026-09-01", "2026-09-30", 20m);
        builder.Recurring("Czynsz", RecurringType.Rent, 400m, "2026-10-01");

        var forecast = ForecastCalculator.Compute(builder.Build());

        forecast.DaysLeft.Should().Be(10);
        forecast.ProjectedEndGr.Should().Be(100_000 - 40_000 - 10 * 2_000);
        forecast.SafeToSpendGr.Should().Be(100_000 - 40_000 - 30_000);
        forecast.SafePerDayGr.Should().Be(3_000);
        forecast.Status.Should().Be(ForecastStatus.Ok);
        forecast.RunOutDate.Should().BeNull();
        forecast.Series.Should().HaveCount(11);
        forecast.Series[^1].BalanceGr.Should().Be(forecast.ProjectedEndGr);
    }

    [Fact]
    public void Running_out_before_payday_is_danger_with_the_first_negative_day()
    {
        var builder = new SnapshotBuilder { Balance = 2100m, Payday = 15 };
        builder.Daily("2026-09-01", "2026-09-30", 100m);
        builder.Recurring("Czynsz", RecurringType.Rent, 1600m, "2026-10-01");
        builder.Recurring("Media Expert", RecurringType.Bnpl, 333m, "2026-10-03");

        var forecast = ForecastCalculator.Compute(builder.Build());

        // 2100 − (1600 + 100) on Oct 1 − 100 on Oct 2 − (333 + 100) on Oct 3 < 0
        forecast.Status.Should().Be(ForecastStatus.Danger);
        forecast.RunOutDate.Should().Be(new DateOnly(2026, 10, 3));
    }

    [Theory]
    [InlineData(20_000, ForecastStatus.Ok)]
    [InlineData(19_999, ForecastStatus.Tight)]
    [InlineData(0, ForecastStatus.Tight)]
    [InlineData(-1, ForecastStatus.Danger)]
    public void Status_thresholds(long projectedEndGr, ForecastStatus expected) =>
        ForecastCalculator.StatusOf(projectedEndGr).Should().Be(expected);
}

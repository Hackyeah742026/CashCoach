using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class SimulatorTests
{
    /// <summary>Safe to spend = 1000 − 400 − 300 = 300 zł.</summary>
    private static SnapshotBuilder Base()
    {
        var builder = new SnapshotBuilder { Balance = 1000m, Payday = 10, Buffer = 300m };
        builder.Daily("2026-09-01", "2026-09-30", 10m);
        builder.Recurring("Czynsz", RecurringType.Rent, 400m, "2026-10-01");
        return builder;
    }

    [Theory]
    [InlineData(200, Verdict.Green, 10_000L, null)]
    [InlineData(300, Verdict.Green, 0L, null)]
    [InlineData(450, Verdict.Yellow, null, 15_000L)]
    [InlineData(700, Verdict.Red, null, 40_000L)]
    public void Purchase_verdict_compares_the_price_with_safe_to_spend_and_buffer(decimal price, Verdict verdict, long? leftAfter, long? shortfall)
    {
        var simulation = Simulator.SimulatePurchase(Base().Build(), (long)(price * 100));

        simulation.Verdict.Should().Be(verdict);
        simulation.LeftAfterGr.Should().Be(leftAfter);
        simulation.ShortfallGr.Should().Be(shortfall);
        simulation.After.ProjectedEndGr.Should().Be(simulation.Before.ProjectedEndGr - (long)(price * 100));
    }

    [Fact]
    public void Purchase_delays_goals_by_whole_months()
    {
        var snapshot = Base().Goal("Laptop", 3000m, 1000m, 500m).Build();

        var delay = Simulator.SimulatePurchase(snapshot, 100_000).GoalDelays.Single();

        delay.ReachDateBefore.Should().Be(new DateOnly(2027, 1, 30));
        delay.ReachDateAfter.Should().Be(new DateOnly(2027, 3, 30));
        delay.ShiftMonths.Should().Be(2);
    }

    [Fact]
    public void Spending_less_per_week_gives_a_monthly_saving_and_reaches_goals_sooner()
    {
        var builder = new SnapshotBuilder().Goal("Wakacje", 2000m, 0m, 200m);
        builder.Daily("2026-09-01", "2026-09-30", 20m, "Glovo", Category.FoodDelivery);

        var change = Simulator.SimulateChange(builder.Build(), Category.FoodDelivery, 4_000);

        change.CurrentPerWeekGr.Should().Be(14_000);
        change.MonthlySavingGr.Should().Be(Money.Round(10_000 * 52m / 12m));
        change.YearlySavingGr.Should().Be(change.MonthlySavingGr * 12);
        change.Goals.Single().ReachDateBefore.Should().Be(new DateOnly(2027, 7, 30));
        change.Goals.Single().ShiftMonths.Should().BeNegative();
    }

    [Fact]
    public void Spending_more_than_now_saves_nothing() =>
        Simulator.SimulateChange(Base().Build(), Category.Groceries, 1_000_000).MonthlySavingGr.Should().Be(0);
}

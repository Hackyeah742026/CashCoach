using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class GoalCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 9, 30);

    [Theory]
    [InlineData(100_000, 50_000, "2026-11-30")]
    [InlineData(100_001, 50_000, "2026-12-30")]
    [InlineData(0, 50_000, "2026-09-30")]
    public void Reach_date_is_today_plus_ceil_remaining_over_plan_months(long remaining, long plan, string expected) =>
        GoalCalculator.ReachDate(Today, remaining, plan).Should().Be(DateOnly.Parse(expected));

    [Fact]
    public void No_plan_means_no_reach_date() => GoalCalculator.ReachDate(Today, 1, 0).Should().BeNull();

    [Fact]
    public void Goal_with_too_small_a_plan_is_behind_by_the_missing_amount()
    {
        var goal = new Goal { Name = "Koncert", TargetGr = 120_000, SavedGr = 54_000, MonthlyPlanGr = 20_000, Deadline = new DateOnly(2026, 12, 31) };

        var progress = GoalCalculator.Progress(goal, Today);

        progress.Status.Should().Be(GoalStatus.Behind);
        progress.RemainingGr.Should().Be(66_000);
        progress.ShortByGr.Should().Be(66_000 - 3 * 20_000);
        progress.RequiredPerWeekGr.Should().Be((long)Math.Ceiling(66_000 * 7m / 92));
        progress.ProgressPct.Should().Be(45m);
    }

    [Fact]
    public void Goal_is_on_track_when_the_plan_covers_it_and_done_when_saved()
    {
        var onTrack = new Goal { TargetGr = 60_000, SavedGr = 0, MonthlyPlanGr = 20_000, Deadline = new DateOnly(2026, 12, 31) };
        var done = new Goal { TargetGr = 60_000, SavedGr = 60_000 };

        GoalCalculator.Progress(onTrack, Today).Status.Should().Be(GoalStatus.OnTrack);
        GoalCalculator.Progress(done, Today).Status.Should().Be(GoalStatus.Done);
    }

    [Fact]
    public void Preview_is_green_when_the_surplus_covers_it_and_yellow_with_a_plan_otherwise()
    {
        var builder = new SnapshotBuilder().Income("2026-09-10", 3000m);
        builder.Daily("2026-09-01", "2026-09-30", 80m, "Glovo", Category.FoodDelivery); // 2400 spent, surplus 600 a month
        var snapshot = builder.Build();
        var opportunities = OpportunityFinder.Find(snapshot);

        GoalPlanner.Preview(snapshot, 50_000, 0, new DateOnly(2026, 12, 29), opportunities).Verdict.Should().Be(Verdict.Green);
        var yellow = GoalPlanner.Preview(snapshot, 500_000, 0, new DateOnly(2027, 3, 29), opportunities);
        yellow.Verdict.Should().Be(Verdict.Yellow);
        yellow.Plan.Should().ContainSingle(o => o.Type == OpportunityType.FoodDelivery);
        GoalPlanner.Preview(snapshot, 5_000_000, 0, new DateOnly(2026, 10, 29), opportunities).Verdict.Should().Be(Verdict.Red);
    }
}

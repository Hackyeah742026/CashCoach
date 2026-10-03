using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

public enum GoalStatus
{
    OnTrack,
    Behind,
    Done,
}

/// <param name="RequiredPerWeekGr">Saving per week needed to hit the deadline; <c>null</c> without a deadline.</param>
/// <param name="ShortByGr">How much the monthly plan leaves missing at the deadline; <c>null</c> unless <see cref="GoalStatus.Behind"/>.</param>
/// <param name="ReachDate">When the monthly plan reaches the target; <c>null</c> without a plan.</param>
public sealed record GoalProgress(
    Goal Goal,
    long RemainingGr,
    decimal ProgressPct,
    GoalStatus Status,
    long? RequiredPerWeekGr,
    long? RequiredPerMonthGr,
    long? ShortByGr,
    DateOnly? ReachDate);

public static class GoalCalculator
{
    /// <summary>today + ceil((target − saved) / monthly plan) months.</summary>
    public static DateOnly? ReachDate(DateOnly asOf, long remainingGr, long monthlyPlanGr)
    {
        if (remainingGr <= 0)
        {
            return asOf;
        }

        if (monthlyPlanGr <= 0)
        {
            return null;
        }

        return asOf.AddMonths((int)Math.Ceiling((decimal)remainingGr / monthlyPlanGr));
    }

    /// <summary>Whole months from <paramref name="from"/> until <paramref name="to"/> (0 when <paramref name="to"/> is within a month).</summary>
    public static int WholeMonthsBetween(DateOnly from, DateOnly to)
    {
        var months = 0;
        while (from.AddMonths(months + 1) <= to)
        {
            months++;
        }

        return months;
    }

    /// <summary>Saving per week needed to collect <paramref name="remainingGr"/> by <paramref name="deadline"/> (at least one week).</summary>
    public static long RequiredPerWeek(DateOnly asOf, DateOnly deadline, long remainingGr)
    {
        var days = Math.Max(7, deadline.DayNumber - asOf.DayNumber);
        return (long)Math.Ceiling(Math.Max(0, remainingGr) * 7m / days);
    }

    /// <summary>Monthly deposits that remain before the deadline: whole months, at least one.</summary>
    public static int MonthsLeft(DateOnly asOf, DateOnly deadline) => Math.Max(1, WholeMonthsBetween(asOf, deadline));

    /// <summary>Saving per month needed to collect <paramref name="remainingGr"/> in <see cref="MonthsLeft"/> deposits.</summary>
    public static long RequiredPerMonth(DateOnly asOf, DateOnly deadline, long remainingGr) =>
        (long)Math.Ceiling(Math.Max(0, remainingGr) / (decimal)MonthsLeft(asOf, deadline));

    public static GoalProgress Progress(Goal goal, DateOnly asOf)
    {
        var remaining = Math.Max(0, goal.TargetGr - goal.SavedGr);
        var pct = goal.TargetGr <= 0 ? 100m : Math.Min(100m, decimal.Round(goal.SavedGr * 100m / goal.TargetGr, 1, MidpointRounding.AwayFromZero));
        var reach = ReachDate(asOf, remaining, goal.MonthlyPlanGr);

        if (remaining == 0)
        {
            return new GoalProgress(goal, 0, pct, GoalStatus.Done, 0, 0, null, asOf);
        }

        if (goal.Deadline is not { } deadline)
        {
            var status = goal.MonthlyPlanGr > 0 ? GoalStatus.OnTrack : GoalStatus.Behind;
            return new GoalProgress(goal, remaining, pct, status, null, null, null, reach);
        }

        var projected = goal.MonthlyPlanGr * MonthsLeft(asOf, deadline);
        var shortBy = remaining - projected;
        return new GoalProgress(
            goal,
            remaining,
            pct,
            shortBy > 0 ? GoalStatus.Behind : GoalStatus.OnTrack,
            RequiredPerWeek(asOf, deadline, remaining),
            RequiredPerMonth(asOf, deadline, remaining),
            shortBy > 0 ? shortBy : null,
            reach);
    }
}

/// <param name="MonthlySurplusGr">Average monthly income minus expenses over the last 90 days (may be negative).</param>
/// <param name="Plan">Opportunities that would close the gap between the surplus and the required saving, largest first.</param>
public sealed record GoalPreview(long RequiredPerWeekGr, long RequiredPerMonthGr, long MonthlySurplusGr, Verdict Verdict, IReadOnlyList<Opportunity> Plan);

public static class GoalPlanner
{
    /// <summary>Green if the monthly surplus covers the goal, yellow if the savings opportunities close the gap, red otherwise.</summary>
    public static GoalPreview Preview(FinancialSnapshot snapshot, long targetGr, long savedGr, DateOnly deadline, IReadOnlyList<Opportunity> opportunities)
    {
        var remaining = Math.Max(0, targetGr - savedGr);
        var perWeek = GoalCalculator.RequiredPerWeek(snapshot.AsOf, deadline, remaining);
        var perMonth = GoalCalculator.RequiredPerMonth(snapshot.AsOf, deadline, remaining);
        var surplus = MonthlySurplus(snapshot);

        if (perMonth <= surplus)
        {
            return new GoalPreview(perWeek, perMonth, surplus, Verdict.Green, []);
        }

        var plan = new List<Opportunity>();
        var covered = surplus;
        foreach (var opportunity in opportunities.OrderByDescending(o => o.MonthlySavingGr))
        {
            plan.Add(opportunity);
            covered += opportunity.MonthlySavingGr;
            if (covered >= perMonth)
            {
                return new GoalPreview(perWeek, perMonth, surplus, Verdict.Yellow, plan);
            }
        }

        return new GoalPreview(perWeek, perMonth, surplus, Verdict.Red, plan);
    }

    public static long MonthlySurplus(FinancialSnapshot snapshot)
    {
        var window = snapshot.RecentWindow(OpportunityFinder.WindowDays);
        var net = snapshot.Transactions.Where(t => t.Date >= window.From && t.Date <= window.To).Sum(t => t.AmountGr);
        return Money.Round(net * 30m / window.Days);
    }
}

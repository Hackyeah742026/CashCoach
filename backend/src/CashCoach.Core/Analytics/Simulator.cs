using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

public enum Verdict
{
    Green,
    Yellow,
    Red,
}

/// <param name="ShiftMonths">Whole months the reach date moves: positive is later, negative is sooner.</param>
public sealed record GoalImpact(Guid GoalId, string Name, DateOnly? ReachDateBefore, DateOnly? ReachDateAfter, int ShiftMonths);

/// <param name="Verdict">Green if the price fits in safe-to-spend, yellow if it fits only by using the safety buffer, red otherwise.</param>
/// <param name="LeftAfterGr">Safe-to-spend left after the purchase, or <c>null</c> when it does not fit.</param>
/// <param name="ShortfallGr">How much safe-to-spend is missing, or <c>null</c> when it fits.</param>
public sealed record PurchaseSimulation(
    long AmountGr,
    DateOnly Date,
    bool BeforePayday,
    Forecast Before,
    Forecast After,
    Verdict Verdict,
    long? LeftAfterGr,
    long? ShortfallGr,
    IReadOnlyList<GoalImpact> GoalDelays);

/// <param name="CurrentPerWeekGr">Average weekly spending in the category over the last 90 days.</param>
public sealed record ChangeSimulation(
    Category Category,
    long CurrentPerWeekGr,
    long NewPerWeekGr,
    long MonthlySavingGr,
    long YearlySavingGr,
    IReadOnlyList<Guid> TransactionIds,
    IReadOnlyList<GoalImpact> Goals)
{
    public long CurrentPerMonthGr => Money.Round(CurrentPerWeekGr * 52m / 12m);
}

/// <summary>"What if" calculations built on the forecast and goal math.</summary>
public static class Simulator
{
    /// <param name="amountGr">Positive price in grosze.</param>
    /// <param name="date">Purchase date; defaults to the day after <see cref="FinancialSnapshot.AsOf"/>.</param>
    public static PurchaseSimulation SimulatePurchase(FinancialSnapshot snapshot, long amountGr, DateOnly? date = null)
    {
        var purchaseDate = date ?? snapshot.AsOf.AddDays(1);
        var before = ForecastCalculator.Compute(snapshot);
        var after = ForecastCalculator.Compute(snapshot, [new UpcomingPayment(purchaseDate, "purchase", RecurringType.Subscription, amountGr, null)]);

        var safe = before.SafeToSpendGr;
        var verdict = amountGr <= safe ? Verdict.Green
            : amountGr <= safe + snapshot.SafetyBufferGr ? Verdict.Yellow
            : Verdict.Red;

        var delays = snapshot.Goals
            .Select(goal =>
            {
                var remaining = Math.Max(0, goal.TargetGr - goal.SavedGr);
                var reachBefore = GoalCalculator.ReachDate(snapshot.AsOf, remaining, goal.MonthlyPlanGr);
                var reachAfter = GoalCalculator.ReachDate(snapshot.AsOf, remaining + amountGr, goal.MonthlyPlanGr);
                return new GoalImpact(goal.Id, goal.Name, reachBefore, reachAfter, MonthsBetween(reachBefore, reachAfter));
            })
            .Where(impact => impact.ShiftMonths > 0)
            .ToList();

        return new PurchaseSimulation(
            amountGr,
            purchaseDate,
            purchaseDate < before.NextPayday,
            before,
            after,
            verdict,
            safe - amountGr >= 0 ? safe - amountGr : null,
            amountGr > safe ? amountGr - Math.Max(0, safe) : null,
            delays);
    }

    /// <param name="newPerWeekGr">Planned weekly spending in <paramref name="category"/>, positive grosze.</param>
    public static ChangeSimulation SimulateChange(FinancialSnapshot snapshot, Category category, long newPerWeekGr)
    {
        var window = snapshot.RecentWindow(OpportunityFinder.WindowDays);
        var spending = snapshot.ExpensesBetween(window.From, window.To).Where(t => t.Category == category).ToList();
        var currentPerWeek = Money.Round(-spending.Sum(t => t.AmountGr) * 7m / window.Days);
        var monthlySaving = Math.Max(0, Money.Round((currentPerWeek - newPerWeekGr) * 52m / 12m));

        var goals = snapshot.Goals
            .Select(goal =>
            {
                var remaining = Math.Max(0, goal.TargetGr - goal.SavedGr);
                var reachBefore = GoalCalculator.ReachDate(snapshot.AsOf, remaining, goal.MonthlyPlanGr);
                var reachAfter = GoalCalculator.ReachDate(snapshot.AsOf, remaining, goal.MonthlyPlanGr + monthlySaving);
                return new GoalImpact(goal.Id, goal.Name, reachBefore, reachAfter, -MonthsBetween(reachAfter, reachBefore));
            })
            .ToList();

        return new ChangeSimulation(
            category, currentPerWeek, newPerWeekGr, monthlySaving, monthlySaving * 12,
            spending.OrderByDescending(t => t.Date).Select(t => t.Id).ToList(), goals);
    }

    private static int MonthsBetween(DateOnly? earlier, DateOnly? later) =>
        earlier is { } from && later is { } to && to > from ? GoalCalculator.WholeMonthsBetween(from, to) : 0;
}

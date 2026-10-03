using CashCoach.Core.Domain;
using CashCoach.Core.Services;

namespace CashCoach.Core.Analytics;

public enum AlertType
{
    RunOut,
    Bnpl,
    DuplicateSub,
    Challenge,
}

public enum AlertSeverity
{
    Info,
    Warning,
    Critical,
}

/// <param name="Id">Stable key used to dismiss the alert, e.g. <c>run_out:2026-10-05</c>.</param>
/// <param name="Subject">Merchant, subscription group or challenge title the alert is about.</param>
/// <param name="AmountGr">The main figure (positive), when there is one.</param>
public sealed record Alert(
    string Id,
    AlertType Type,
    AlertSeverity Severity,
    string? Subject,
    DateOnly? Date,
    long? AmountGr,
    IReadOnlyList<Guid> TransactionIds);

public static class AlertBuilder
{
    public const int BnplLookaheadDays = 7;

    public static IReadOnlyList<Alert> Build(FinancialSnapshot snapshot, Forecast forecast, IEnumerable<Challenge> challenges)
    {
        var alerts = new List<Alert>();

        if (forecast.Status == ForecastStatus.Danger)
        {
            alerts.Add(new Alert(
                $"run_out:{forecast.RunOutDate:yyyy-MM-dd}", AlertType.RunOut, AlertSeverity.Critical, null,
                forecast.RunOutDate, -forecast.ProjectedEndGr, forecast.UpcomingTransactionIds));
        }

        foreach (var plan in BnplPlanBuilder.Build(snapshot.Transactions).Where(p => p.Active && p.NextDate is { } next
                     && next > snapshot.AsOf && next.DayNumber - snapshot.AsOf.DayNumber <= BnplLookaheadDays))
        {
            alerts.Add(new Alert(
                $"bnpl:{plan.Merchant}:{plan.NextDate:yyyy-MM-dd}", AlertType.Bnpl, AlertSeverity.Warning, plan.Merchant,
                plan.NextDate, plan.InstalmentGr, plan.TransactionIds));
        }

        foreach (var duplicate in OpportunityFinder.Find(snapshot).Where(o => o.Type == OpportunityType.DuplicateSubscription))
        {
            alerts.Add(new Alert(
                $"duplicate_sub:{duplicate.Id.Split(':')[1]}", AlertType.DuplicateSub, AlertSeverity.Info, duplicate.Subject,
                null, duplicate.MonthlySavingGr, duplicate.TransactionIds));
        }

        foreach (var challenge in challenges.Where(c => c.Status == ChallengeStatuses.Active))
        {
            var breaks = ChallengeTracker.UncountedBreaks(challenge, snapshot.Transactions);
            if (breaks.Count > 0)
            {
                alerts.Add(new Alert(
                    $"challenge:{challenge.Id}:{breaks[0].Date:yyyy-MM-dd}", AlertType.Challenge, AlertSeverity.Warning, challenge.Title,
                    breaks[0].Date, -breaks.Sum(t => t.AmountGr), breaks.Select(t => t.Id).ToList()));
            }
        }

        return alerts.OrderBy(a => a.Severity switch { AlertSeverity.Critical => 0, AlertSeverity.Warning => 1, _ => 2 }).ToList();
    }
}

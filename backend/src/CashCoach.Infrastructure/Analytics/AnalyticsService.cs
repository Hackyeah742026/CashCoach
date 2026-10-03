using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Analytics;

/// <summary>Opportunities and alerts with the user's dismissals applied.</summary>
public sealed class AnalyticsService(AppDbContext db, DismissalService dismissals)
{
    public async Task<IReadOnlyList<Opportunity>> OpportunitiesAsync(FinancialSnapshot snapshot, Guid userId, CancellationToken cancellationToken)
    {
        var dismissed = await dismissals.KeysAsync(userId, cancellationToken);
        return OpportunityFinder.Find(snapshot)
            .Where(o => !dismissed.Contains(DismissalService.OpportunityPrefix + o.Id))
            .Take(OpportunityFinder.Top)
            .ToList();
    }

    public async Task<IReadOnlyList<Alert>> AlertsAsync(FinancialSnapshot snapshot, Forecast forecast, Guid userId, CancellationToken cancellationToken)
    {
        var dismissed = await dismissals.KeysAsync(userId, cancellationToken);
        var challenges = await db.Challenges.AsNoTracking().Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        return AlertBuilder.Build(snapshot, forecast, challenges)
            .Where(a => !dismissed.Contains(DismissalService.AlertPrefix + a.Id))
            .ToList();
    }

    /// <returns><c>null</c> when the user has no transactions in <paramref name="month"/>.</returns>
    public async Task<WrappedStats?> WrappedAsync(FinancialSnapshot snapshot, Guid userId, DateOnly month, CancellationToken cancellationToken)
    {
        if (!WrappedBuilder.AvailableMonths(snapshot.Transactions).Contains(month))
        {
            return null;
        }

        var potential = (await OpportunitiesAsync(snapshot, userId, cancellationToken)).Sum(o => o.MonthlySavingGr);
        return WrappedBuilder.Build(snapshot.Transactions, month, potential);
    }

    public static SubscriptionOverview Subscriptions(FinancialSnapshot snapshot) =>
        SubscriptionOverviewCalculator.Build(snapshot.Recurring.Where(g => g.Type == RecurringType.Subscription));

    public static BnplSummary Bnpl(FinancialSnapshot snapshot) => new(BnplPlanBuilder.Build(snapshot.Transactions));
}

using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Insights;

/// <summary>Loads a user's data and hands it to the Core calculators.</summary>
public sealed class InsightService(AppDbContext db, TimeProvider timeProvider)
{
    /// <summary>Periods are relative to the user's latest transaction, so an imported history is summarized as of its end.</summary>
    public async Task<SpendingSummary> GetSummaryAsync(Guid userId, SummaryPeriod period, CancellationToken cancellationToken)
    {
        var referenceDate = await db.Transactions.Where(t => t.UserId == userId).MaxAsync(t => (DateOnly?)t.Date, cancellationToken)
            ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var (current, previous) = SummaryPeriods.Resolve(period, referenceDate);

        var expenses = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.AmountGr < 0 && t.Date >= previous.From && t.Date <= current.To)
            .ToListAsync(cancellationToken);

        return SpendingSummaryCalculator.Summarize(expenses, period, referenceDate);
    }

    public async Task<SubscriptionOverview> GetSubscriptionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var subscriptions = await db.RecurringGroups.AsNoTracking()
            .Where(g => g.UserId == userId && g.Type == RecurringType.Subscription && g.Active)
            .ToListAsync(cancellationToken);
        return SubscriptionOverviewCalculator.Build(subscriptions);
    }

    /// <returns>The updated subscription, or <c>null</c> when the user has no such active subscription.</returns>
    public async Task<RecurringGroup?> SetStillUsingAsync(Guid userId, Guid subscriptionId, bool stillUsing, CancellationToken cancellationToken)
    {
        var subscription = await db.RecurringGroups.SingleOrDefaultAsync(
            g => g.Id == subscriptionId && g.UserId == userId && g.Type == RecurringType.Subscription, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        subscription.UserConfirmed = stillUsing;
        await db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task<BnplSummary> GetBnplAsync(Guid userId, CancellationToken cancellationToken)
    {
        var instalments = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.IsBnpl && t.AmountGr < 0)
            .ToListAsync(cancellationToken);
        return new BnplSummary(BnplPlanBuilder.Build(instalments));
    }
}

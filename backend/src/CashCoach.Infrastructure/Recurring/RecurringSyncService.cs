using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Recurring;

/// <param name="Subscriptions">Active subscriptions after the refresh.</param>
/// <param name="Bnpl">Active BNPL plans after the refresh.</param>
public sealed record RecurringSyncResult(int Subscriptions, int Bnpl, int? SalaryDay);

/// <summary>Re-runs recurring detection over all of a user's transactions and stores the result.</summary>
public sealed class RecurringSyncService(AppDbContext db)
{
    /// <summary>
    /// Existing groups are matched by merchant and type, so their id and <see cref="RecurringGroup.UserConfirmed"/> survive a re-import.
    /// Groups no longer detected are deactivated rather than deleted.
    /// </summary>
    public async Task<RecurringSyncResult> RefreshAsync(Guid userId, CancellationToken cancellationToken)
    {
        var transactions = await db.Transactions.Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        var existing = await db.RecurringGroups.Where(g => g.UserId == userId).ToListAsync(cancellationToken);
        var detection = RecurringDetector.Detect(transactions);

        var transactionsById = transactions.ToDictionary(t => t.Id);
        foreach (var transaction in transactions)
        {
            transaction.IsRecurring = false;
            transaction.RecurringGroupId = null;
        }

        var unmatched = existing.ToList();
        foreach (var detected in detection.Groups)
        {
            RecurringGroup? group = null;
            if (detected.Type is { } type)
            {
                group = unmatched.FirstOrDefault(g => g.Type == type && string.Equals(g.Merchant, detected.Merchant, StringComparison.OrdinalIgnoreCase));
                if (group is null)
                {
                    group = new RecurringGroup { Id = Guid.NewGuid(), UserId = userId, Type = type };
                    db.RecurringGroups.Add(group);
                }
                else
                {
                    unmatched.Remove(group);
                }

                group.Merchant = detected.Merchant;
                group.AvgAmountGr = detected.AvgAmountGr;
                group.PeriodDays = detected.PeriodDays;
                group.NextDate = detected.NextDate;
                group.Active = detected.Active;
            }

            foreach (var id in detected.TransactionIds)
            {
                var transaction = transactionsById[id];
                transaction.IsRecurring = true;
                transaction.RecurringGroupId = group?.Id;
            }
        }

        foreach (var stale in unmatched)
        {
            stale.Active = false;
        }

        await db.SaveChangesAsync(cancellationToken);

        var active = detection.Groups.Where(g => g.Active).ToList();
        return new RecurringSyncResult(
            active.Count(g => g.Type == RecurringType.Subscription),
            active.Count(g => g.Type == RecurringType.Bnpl),
            detection.SalaryDay);
    }
}

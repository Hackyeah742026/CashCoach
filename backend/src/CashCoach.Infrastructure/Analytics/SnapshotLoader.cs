using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Analytics;

/// <summary>Loads a <see cref="FinancialSnapshot"/> for one user (read-only).</summary>
public sealed class SnapshotLoader(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<FinancialSnapshot> LoadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId, cancellationToken);
        var transactions = await db.Transactions.AsNoTracking().Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        var recurring = await db.RecurringGroups.AsNoTracking().Where(g => g.UserId == userId && g.Active).ToListAsync(cancellationToken);
        var goals = await db.Goals.AsNoTracking().Where(g => g.UserId == userId).OrderBy(g => g.CreatedAt).ToListAsync(cancellationToken);

        var asOf = transactions.Count > 0 ? transactions.Max(t => t.Date) : DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        return new FinancialSnapshot(asOf, BalanceOf(user, transactions), user.Payday, user.SafetyBufferGr, transactions, recurring, goals);
    }

    /// <summary>The user's balance, or when unset an estimate: the net of the imported history, never below zero.</summary>
    public static long BalanceOf(User user, IEnumerable<Transaction> transactions) =>
        user.BalanceGr ?? Math.Max(0, transactions.Sum(t => t.AmountGr));
}

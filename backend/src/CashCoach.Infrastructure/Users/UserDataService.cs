using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Users;

/// <summary>Everything stored about a user, for <c>GET /me/export</c> (GDPR data portability).</summary>
public sealed record UserExport(
    DateTime ExportedAt,
    User User,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<RecurringGroup> RecurringGroups,
    IReadOnlyList<Goal> Goals,
    IReadOnlyList<Challenge> Challenges,
    IReadOnlyList<ChatMessage> ChatMessages,
    IReadOnlyList<UserMerchantRule> MerchantRules,
    IReadOnlyList<Dismissal> Dismissals);

public sealed class UserDataService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<UserExport> ExportAsync(User user, CancellationToken cancellationToken) => new(
        timeProvider.GetUtcNow().UtcDateTime,
        user,
        await db.Transactions.AsNoTracking().Where(t => t.UserId == user.Id).OrderBy(t => t.Date).ToListAsync(cancellationToken),
        await db.RecurringGroups.AsNoTracking().Where(g => g.UserId == user.Id).ToListAsync(cancellationToken),
        await db.Goals.AsNoTracking().Where(g => g.UserId == user.Id).ToListAsync(cancellationToken),
        await db.Challenges.AsNoTracking().Where(c => c.UserId == user.Id).ToListAsync(cancellationToken),
        await db.ChatMessages.AsNoTracking().Where(m => m.UserId == user.Id).OrderBy(m => m.CreatedAt).ToListAsync(cancellationToken),
        await db.UserMerchantRules.AsNoTracking().Where(r => r.UserId == user.Id).ToListAsync(cancellationToken),
        await db.Dismissals.AsNoTracking().Where(d => d.UserId == user.Id).ToListAsync(cancellationToken));

    /// <summary>Deletes the user; every owned row goes with it through cascade deletes.</summary>
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        // Transactions reference recurring groups, so drop them first to keep SQLite's FK order simple.
        await db.Transactions.Where(t => t.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);
    }
}

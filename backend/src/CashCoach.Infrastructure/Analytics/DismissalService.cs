using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Analytics;

public sealed class DismissalService(AppDbContext db, TimeProvider timeProvider)
{
    public const string AlertPrefix = "alert:";
    public const string OpportunityPrefix = "opportunity:";

    public async Task<HashSet<string>> KeysAsync(Guid userId, CancellationToken cancellationToken) =>
        (await db.Dismissals.AsNoTracking().Where(d => d.UserId == userId).Select(d => d.Key).ToListAsync(cancellationToken)).ToHashSet();

    public async Task DismissAsync(Guid userId, string key, CancellationToken cancellationToken)
    {
        if (await db.Dismissals.AnyAsync(d => d.UserId == userId && d.Key == key, cancellationToken))
        {
            return;
        }

        db.Dismissals.Add(new Dismissal { Id = Guid.NewGuid(), UserId = userId, Key = key, CreatedAt = timeProvider.GetUtcNow().UtcDateTime });
        await db.SaveChangesAsync(cancellationToken);
    }
}

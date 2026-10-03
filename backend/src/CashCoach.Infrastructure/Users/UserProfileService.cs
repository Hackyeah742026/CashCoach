using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Users;

public sealed record UserProfile(User User, bool HasData);

public sealed class UserProfileService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<UserProfile> GetAsync(User user, CancellationToken cancellationToken) =>
        new(user, await db.Transactions.AnyAsync(t => t.UserId == user.Id, cancellationToken));

    /// <summary>Updates the given fields of a user tracked by this context; <c>null</c> leaves a field unchanged.</summary>
    public async Task<UserProfile> UpdateAsync(User user, string? name, string? language, CancellationToken cancellationToken)
    {
        user.Name = name ?? user.Name;
        user.Language = language ?? user.Language;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(user, cancellationToken);
    }

    public async Task<DateTime> AcceptConsentAsync(User user, CancellationToken cancellationToken)
    {
        user.ConsentAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return user.ConsentAt.Value;
    }
}

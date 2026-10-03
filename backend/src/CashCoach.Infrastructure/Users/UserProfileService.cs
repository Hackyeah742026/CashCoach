using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Users;

/// <param name="AsOf">The user's latest transaction date ("today" for the analytics), or <c>null</c> without data.</param>
/// <param name="BalanceGr">The set balance, or the estimate used when none is set.</param>
public sealed record UserProfile(User User, bool HasData, DateOnly? AsOf, long BalanceGr, IReadOnlyList<DateOnly> AvailableMonths);

/// <summary>Profile fields to change; <c>null</c> keeps the current value.</summary>
public sealed record ProfileUpdate(string? Name, string? Language, int? Payday, long? SafetyBufferGr, long? BalanceGr);

public sealed class UserProfileService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<UserProfile> GetAsync(User user, CancellationToken cancellationToken)
    {
        var rows = await db.Transactions.Where(t => t.UserId == user.Id)
            .Select(t => new { t.Date, t.AmountGr })
            .ToListAsync(cancellationToken);
        var months = rows.Select(r => new DateOnly(r.Date.Year, r.Date.Month, 1)).Distinct().Order().ToList();
        var balance = user.BalanceGr ?? Math.Max(0, rows.Sum(r => r.AmountGr));
        return new UserProfile(user, rows.Count > 0, rows.Count > 0 ? rows.Max(r => r.Date) : null, balance, months);
    }

    /// <summary>Updates the given fields of a user tracked by this context.</summary>
    public async Task<UserProfile> UpdateAsync(User user, ProfileUpdate update, CancellationToken cancellationToken)
    {
        user.Name = update.Name ?? user.Name;
        user.Language = update.Language ?? user.Language;
        user.Payday = update.Payday ?? user.Payday;
        user.SafetyBufferGr = update.SafetyBufferGr ?? user.SafetyBufferGr;
        user.BalanceGr = update.BalanceGr ?? user.BalanceGr;
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

using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Analytics;

public sealed record ChallengeCheckIn(Challenge Challenge, CheckInResult Result);

/// <summary>A second check-in on the same calendar day.</summary>
public sealed class AlreadyCheckedInException() : Exception("Already checked in today.");

public sealed class ChallengeService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<List<Challenge>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Challenges.AsNoTracking().Where(c => c.UserId == userId).OrderByDescending(c => c.StartDate).ToListAsync(cancellationToken);

    /// <summary>Starts today (the real calendar day): only transactions from today on can break it.</summary>
    public async Task<Challenge> CreateAsync(Guid userId, string type, int days, string language, CancellationToken cancellationToken)
    {
        var today = Today();
        var challenge = new Challenge
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = CoachTexts.ChallengeTitle(type, days, language),
            StartDate = today,
            EndDate = today.AddDays(days - 1),
            Target = days,
            Status = ChallengeStatuses.Active,
        };
        db.Challenges.Add(challenge);
        await db.SaveChangesAsync(cancellationToken);
        return challenge;
    }

    /// <returns><c>null</c> when the user has no such challenge.</returns>
    /// <exception cref="AlreadyCheckedInException">The challenge was already checked in today.</exception>
    public async Task<ChallengeCheckIn?> CheckInAsync(Guid userId, Guid challengeId, CancellationToken cancellationToken)
    {
        var challenge = await db.Challenges.SingleOrDefaultAsync(c => c.Id == challengeId && c.UserId == userId, cancellationToken);
        if (challenge is null)
        {
            return null;
        }

        var today = Today();
        if (challenge.LastCheckInOn == today)
        {
            throw new AlreadyCheckedInException();
        }

        var transactions = await db.Transactions.AsNoTracking()
            .Where(t => t.UserId == userId && t.AmountGr < 0 && t.Date >= challenge.StartDate)
            .ToListAsync(cancellationToken);
        var result = ChallengeTracker.CheckIn(challenge, transactions, today);
        await db.SaveChangesAsync(cancellationToken);
        return new ChallengeCheckIn(challenge, result);
    }

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
}

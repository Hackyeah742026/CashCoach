using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

public static class ChallengeTypes
{
    /// <summary>No food delivery; any delivery payment breaks the streak.</summary>
    public const string NoDelivery = "no_delivery";

    /// <summary>No taxi rides (Bolt, Uber, FreeNow…).</summary>
    public const string NoTaxi = "no_taxi";

    public static readonly IReadOnlyList<string> All = [NoDelivery, NoTaxi];

    public static bool Breaks(string type, Transaction transaction) => transaction.AmountGr < 0 && type switch
    {
        NoDelivery => transaction.Category == Category.FoodDelivery,
        NoTaxi => OpportunityFinder.IsTaxi(transaction),
        _ => false,
    };
}

/// <param name="Broken">The check-in found a new breaking transaction and reset the streak.</param>
public sealed record CheckInResult(bool Broken, DateOnly? BreakDate, IReadOnlyList<Guid> BreakingTransactionIds);

public static class ChallengeTracker
{
    /// <summary>Breaking transactions on or after the start that the challenge has not counted yet.</summary>
    public static IReadOnlyList<Transaction> UncountedBreaks(Challenge challenge, IEnumerable<Transaction> transactions) => transactions
        .Where(t => t.Date >= challenge.StartDate && ChallengeTypes.Breaks(challenge.Type, t))
        .Where(t => challenge.LastBreakDate is null || t.Date > challenge.LastBreakDate)
        .OrderByDescending(t => t.Date)
        .ToList();

    /// <summary>
    /// A daily check-in: a new breaking transaction resets the streak to 0, otherwise the streak grows by one.
    /// <see cref="Challenge.Progress"/> keeps the best streak; reaching the target completes the challenge.
    /// </summary>
    public static CheckInResult CheckIn(Challenge challenge, IEnumerable<Transaction> transactions, DateOnly today)
    {
        var breaks = UncountedBreaks(challenge, transactions);
        challenge.LastCheckInOn = today;

        if (breaks.Count > 0)
        {
            challenge.Streak = 0;
            challenge.LastBreakDate = breaks[0].Date;
            return new CheckInResult(true, breaks[0].Date, breaks.Select(t => t.Id).ToList());
        }

        challenge.Streak++;
        challenge.Progress = Math.Max(challenge.Progress, challenge.Streak);
        if (challenge.Streak >= challenge.Target)
        {
            challenge.Status = ChallengeStatuses.Completed;
        }
        else if (today > challenge.EndDate)
        {
            challenge.Status = ChallengeStatuses.Failed;
        }

        return new CheckInResult(false, null, []);
    }
}

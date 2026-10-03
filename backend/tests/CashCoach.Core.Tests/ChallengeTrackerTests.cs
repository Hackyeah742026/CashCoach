using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class ChallengeTrackerTests
{
    private static Challenge NoDelivery() => new()
    {
        Type = ChallengeTypes.NoDelivery,
        StartDate = new DateOnly(2026, 10, 1),
        EndDate = new DateOnly(2026, 10, 3),
        Target = 3,
    };

    [Fact]
    public void Check_ins_grow_the_streak_until_the_target_completes_it()
    {
        var challenge = NoDelivery();

        ChallengeTracker.CheckIn(challenge, [], new DateOnly(2026, 10, 1)).Broken.Should().BeFalse();
        ChallengeTracker.CheckIn(challenge, [], new DateOnly(2026, 10, 2));
        ChallengeTracker.CheckIn(challenge, [], new DateOnly(2026, 10, 3));

        challenge.Streak.Should().Be(3);
        challenge.Status.Should().Be(ChallengeStatuses.Completed);
    }

    [Fact]
    public void A_delivery_payment_resets_the_streak_once()
    {
        var challenge = NoDelivery();
        var builder = new SnapshotBuilder();
        builder.Spend("2026-09-30", 30m, "Glovo", Category.FoodDelivery);
        var delivery = builder.Expense("2026-10-02", 45m, "Glovo", Category.FoodDelivery);
        builder.Spend("2026-10-02", 30m, "Lidl");
        var transactions = builder.Build().Transactions;

        ChallengeTracker.CheckIn(challenge, [], new DateOnly(2026, 10, 1));
        var broken = ChallengeTracker.CheckIn(challenge, transactions, new DateOnly(2026, 10, 2));
        var next = ChallengeTracker.CheckIn(challenge, transactions, new DateOnly(2026, 10, 3));

        broken.Broken.Should().BeTrue();
        broken.BreakingTransactionIds.Should().Equal(delivery.Id);
        next.Broken.Should().BeFalse();
        challenge.Streak.Should().Be(1);
        challenge.Progress.Should().Be(1);
        challenge.Status.Should().Be(ChallengeStatuses.Active);
    }
}

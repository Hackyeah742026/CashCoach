namespace CashCoach.Core.Domain;

public class Challenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>A <see cref="ChallengeTypes"/> value, e.g. <c>no_delivery</c>.</summary>
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Days in a row to reach.</summary>
    public long Target { get; set; }

    /// <summary>Days in a row at the last check-in.</summary>
    public long Progress { get; set; }

    public int Streak { get; set; }

    /// <summary>Date of the latest breaking transaction already counted, so it resets the streak only once.</summary>
    public DateOnly? LastBreakDate { get; set; }

    /// <summary>Calendar day of the last check-in; one check-in per day.</summary>
    public DateOnly? LastCheckInOn { get; set; }

    /// <summary><c>active</c>, <c>completed</c> or <c>failed</c>.</summary>
    public string Status { get; set; } = ChallengeStatuses.Active;
}

public static class ChallengeStatuses
{
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Failed = "failed";
}

namespace CashCoach.Core.Domain;

public class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public string? Emoji { get; set; }
    public long TargetGr { get; set; }
    public long SavedGr { get; set; }
    public DateOnly? Deadline { get; set; }

    /// <summary>How much the user plans to put aside each month, in grosze.</summary>
    public long MonthlyPlanGr { get; set; }

    public DateTime CreatedAt { get; set; }
}

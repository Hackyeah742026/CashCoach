namespace CashCoach.Core.Domain;

public class Challenge
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public long Target { get; set; }
    public long Progress { get; set; }
    public int Streak { get; set; }
    public string Status { get; set; } = "";
}

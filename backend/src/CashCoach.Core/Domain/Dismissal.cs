namespace CashCoach.Core.Domain;

/// <summary>A user dismissed an alert or savings opportunity, identified by its stable key (e.g. <c>alert:run_out</c>).</summary>
public class Dismissal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Key { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

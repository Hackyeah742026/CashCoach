namespace CashCoach.Core.Domain;

public class RecurringGroup
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Merchant { get; set; } = "";
    public RecurringType Type { get; set; }
    public long AvgAmountGr { get; set; }
    public int PeriodDays { get; set; }
    public DateOnly? NextDate { get; set; }
    public bool Active { get; set; } = true;

    /// <summary>The user's answer to "still using it?"; <c>null</c> until they answer.</summary>
    public bool? UserConfirmed { get; set; }
}

namespace CashCoach.Core.Domain;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>Amount in grosze. Expenses are negative, income positive.</summary>
    public long AmountGr { get; set; }

    public string RawDescription { get; set; } = "";
    public string Merchant { get; set; } = "";
    public Category Category { get; set; } = Category.Other;
    public Channel Channel { get; set; }
    public bool IsRecurring { get; set; }
    public Guid? RecurringGroupId { get; set; }
    public bool IsBnpl { get; set; }
}

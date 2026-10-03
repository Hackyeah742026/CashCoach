namespace CashCoach.Core.Domain;

/// <summary>A user's category override for every transaction of one merchant.</summary>
public class UserMerchantRule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Merchant { get; set; } = "";
    public Category Category { get; set; }
}

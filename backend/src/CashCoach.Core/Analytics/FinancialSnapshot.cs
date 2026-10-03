using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

/// <summary>Everything the analytics need about one user, loaded once per request.</summary>
/// <param name="AsOf">"Today" for the analytics: the user's latest transaction date, so an imported history is analysed as of its end.</param>
/// <param name="BalanceGr">Account balance at <paramref name="AsOf"/> in grosze.</param>
/// <param name="Payday">Day of month the salary arrives, or <c>null</c> when unknown.</param>
/// <param name="Recurring">Active recurring groups.</param>
public sealed record FinancialSnapshot(
    DateOnly AsOf,
    long BalanceGr,
    int? Payday,
    long SafetyBufferGr,
    IReadOnlyList<Transaction> Transactions,
    IReadOnlyList<RecurringGroup> Recurring,
    IReadOnlyList<Goal> Goals)
{
    /// <summary>Expenses (negative amounts) in the inclusive range.</summary>
    public IEnumerable<Transaction> ExpensesBetween(DateOnly from, DateOnly to) =>
        Transactions.Where(t => t.AmountGr < 0 && t.Date >= from && t.Date <= to);

    /// <summary>
    /// The last <paramref name="days"/> days up to <see cref="AsOf"/>, clipped to the first transaction so a short history
    /// is not averaged over days without data.
    /// </summary>
    public DateRangeDays RecentWindow(int days)
    {
        var from = AsOf.AddDays(-(days - 1));
        if (Transactions.Count > 0)
        {
            var first = Transactions.Min(t => t.Date);
            if (first > from)
            {
                from = first;
            }
        }

        return new DateRangeDays(from, AsOf);
    }
}

public sealed record DateRangeDays(DateOnly From, DateOnly To)
{
    public int Days => Math.Max(1, To.DayNumber - From.DayNumber + 1);
}

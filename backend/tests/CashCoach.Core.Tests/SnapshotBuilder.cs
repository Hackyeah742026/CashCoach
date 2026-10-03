using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;

namespace CashCoach.Core.Tests;

/// <summary>Builds small <see cref="FinancialSnapshot"/>s for analytics tests. Amounts are in złoty.</summary>
internal sealed class SnapshotBuilder
{
    private readonly List<Transaction> _transactions = [];
    private readonly List<RecurringGroup> _recurring = [];
    private readonly List<Goal> _goals = [];

    public DateOnly AsOf { get; init; } = new(2026, 9, 30);
    public decimal Balance { get; init; } = 1000m;
    public int? Payday { get; init; } = 10;
    public decimal Buffer { get; init; } = 300m;

    public Transaction Expense(string date, decimal zloty, string merchant, Category category, bool recurring = false, bool bnpl = false, Guid? groupId = null)
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            Date = DateOnly.Parse(date),
            AmountGr = -(long)(zloty * 100),
            Merchant = merchant,
            RawDescription = merchant.ToUpperInvariant(),
            Category = category,
            IsRecurring = recurring,
            IsBnpl = bnpl,
            RecurringGroupId = groupId,
        };
        _transactions.Add(transaction);
        return transaction;
    }

    public SnapshotBuilder Income(string date, decimal zloty, string merchant = "Wynagrodzenie")
    {
        _transactions.Add(new Transaction { Id = Guid.NewGuid(), Date = DateOnly.Parse(date), AmountGr = (long)(zloty * 100), Merchant = merchant, Category = Category.Salary });
        return this;
    }

    public SnapshotBuilder Spend(string date, decimal zloty, string merchant = "Żabka", Category category = Category.Groceries)
    {
        Expense(date, zloty, merchant, category);
        return this;
    }

    /// <summary>The same expense every day from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public SnapshotBuilder Daily(string from, string to, decimal zloty, string merchant = "Żabka", Category category = Category.Groceries)
    {
        for (var day = DateOnly.Parse(from); day <= DateOnly.Parse(to); day = day.AddDays(1))
        {
            Expense($"{day:yyyy-MM-dd}", zloty, merchant, category);
        }

        return this;
    }

    public RecurringGroup Recurring(string merchant, RecurringType type, decimal zloty, string nextDate, bool? stillUsing = null, int periodDays = 30)
    {
        var group = new RecurringGroup
        {
            Id = Guid.NewGuid(),
            Merchant = merchant,
            Type = type,
            AvgAmountGr = -(long)(zloty * 100),
            PeriodDays = periodDays,
            NextDate = DateOnly.Parse(nextDate),
            Active = true,
            UserConfirmed = stillUsing,
        };
        _recurring.Add(group);
        return group;
    }

    public SnapshotBuilder Goal(string name, decimal target, decimal saved, decimal monthlyPlan, string? deadline = null)
    {
        _goals.Add(new Goal
        {
            Id = Guid.NewGuid(),
            Name = name,
            TargetGr = (long)(target * 100),
            SavedGr = (long)(saved * 100),
            MonthlyPlanGr = (long)(monthlyPlan * 100),
            Deadline = deadline is null ? null : DateOnly.Parse(deadline),
        });
        return this;
    }

    public FinancialSnapshot Build() =>
        new(AsOf, (long)(Balance * 100), Payday, (long)(Buffer * 100), _transactions, _recurring, _goals);
}

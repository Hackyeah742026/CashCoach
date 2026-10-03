using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

public sealed record CategoryShare(Category Category, long AmountGr, int SharePct);

public sealed record MerchantStat(string Name, int Count, long AmountGr, IReadOnlyList<Guid> TransactionIds);

public sealed record DeliveryStat(int Count, long AmountGr, IReadOnlyList<Guid> TransactionIds);

public sealed record DayStat(DateOnly Date, long AmountGr, int Count, IReadOnlyList<Guid> TransactionIds);

public sealed record SubscriptionStat(int Count, long AmountGr, IReadOnlyList<string> Merchants, IReadOnlyList<Guid> TransactionIds);

public sealed record CategoryChange(Category Category, long FromGr, long ToGr, int ChangePct);

/// <param name="Item"><c>pizza</c> or <c>coffee</c>.</param>
public sealed record FunEquivalent(string Item, int Count, long UnitPriceGr, long BasisGr);

/// <param name="Key">Stable personality id, e.g. <c>delivery_lover</c>.</param>
public sealed record Personality(string Key, string Emoji);

/// <param name="ChangePct">Change of total spending against the previous month; <c>null</c> without a previous month.</param>
/// <param name="CheapestWeekday">Weekday with the lowest average non-recurring spending.</param>
public sealed record WrappedStats(
    DateOnly Month,
    long TotalSpentGr,
    long TotalIncomeGr,
    long PreviousSpentGr,
    int? ChangePct,
    int TransactionCount,
    IReadOnlyList<CategoryShare> TopCategories,
    MerchantStat? TopMerchant,
    DeliveryStat Delivery,
    DayStat? BiggestDay,
    SubscriptionStat Subscriptions,
    CategoryChange? BiggestChange,
    DayOfWeek CheapestWeekday,
    FunEquivalent Fun,
    Personality Personality,
    long PotentialSavingsGr);

public static class WrappedCardTypes
{
    public const string TotalSpent = "total_spent";
    public const string TopCategories = "top_categories";
    public const string TopMerchant = "top_merchant";
    public const string Delivery = "delivery";
    public const string BiggestDay = "biggest_day";
    public const string Subscriptions = "subscriptions";
    public const string MonthOverMonth = "month_over_month";
    public const string Personality = "personality";

    public static readonly IReadOnlyList<string> All =
        [TotalSpent, TopCategories, TopMerchant, Delivery, BiggestDay, Subscriptions, MonthOverMonth, Personality];
}

/// <summary>Monthly "Wrapped" statistics; every number is computed here.</summary>
public static class WrappedBuilder
{
    public const long PizzaPriceGr = 3_500;
    public const long CoffeePriceGr = 1_500;

    private static readonly Category[] NonDiscretionary = [Category.Rent, Category.Utilities, Category.Transfers];

    public static IReadOnlyList<DateOnly> AvailableMonths(IEnumerable<Transaction> transactions) => transactions
        .Select(t => new DateOnly(t.Date.Year, t.Date.Month, 1))
        .Distinct()
        .OrderByDescending(month => month)
        .ToList();

    /// <param name="month">Any day in the month.</param>
    /// <param name="potentialSavingsGr">Monthly total of the current savings opportunities.</param>
    public static WrappedStats Build(IReadOnlyList<Transaction> transactions, DateOnly month, long potentialSavingsGr)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var inMonth = transactions.Where(t => t.Date >= start && t.Date <= end).ToList();
        var expenses = inMonth.Where(t => t.AmountGr < 0).ToList();
        var previous = transactions.Where(t => t.AmountGr < 0 && t.Date >= start.AddMonths(-1) && t.Date < start).ToList();

        var totalSpent = -expenses.Sum(t => t.AmountGr);
        var previousSpent = -previous.Sum(t => t.AmountGr);

        var delivery = expenses.Where(t => t.Category == Category.FoodDelivery).ToList();
        var deliveryStat = new DeliveryStat(delivery.Count, -delivery.Sum(t => t.AmountGr), Ids(delivery));

        var subscriptions = expenses.Where(t => t.Category == Category.Subscriptions).ToList();
        var subscriptionStat = new SubscriptionStat(
            subscriptions.Select(t => t.Merchant).Distinct().Count(),
            -subscriptions.Sum(t => t.AmountGr),
            subscriptions.Select(t => t.Merchant).Distinct().Order(StringComparer.Ordinal).ToList(),
            Ids(subscriptions));

        var fun = deliveryStat.AmountGr > 0
            ? new FunEquivalent("pizza", (int)(deliveryStat.AmountGr / PizzaPriceGr), PizzaPriceGr, deliveryStat.AmountGr)
            : new FunEquivalent("coffee", (int)(totalSpent / CoffeePriceGr), CoffeePriceGr, totalSpent);

        return new WrappedStats(
            start,
            totalSpent,
            inMonth.Where(t => t.AmountGr > 0).Sum(t => t.AmountGr),
            previousSpent,
            previousSpent == 0 ? null : Pct(totalSpent - previousSpent, previousSpent),
            inMonth.Count,
            TopCategories(expenses, totalSpent),
            TopMerchant(expenses),
            deliveryStat,
            BiggestDay(expenses),
            subscriptionStat,
            BiggestChange(expenses, previous),
            CheapestWeekday(expenses, start, end),
            fun,
            PersonalityOf(expenses, totalSpent),
            potentialSavingsGr);
    }

    /// <summary>Spending per category in the month (positive), largest first, with the change against the previous month (<c>null</c> when nothing was spent then).</summary>
    public static IReadOnlyList<(Category Category, long AmountGr, decimal? ChangePct, IReadOnlyList<Guid> TransactionIds)> CategoryChanges(
        IReadOnlyList<Transaction> transactions, DateOnly month)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var before = transactions.Where(t => t.AmountGr < 0 && t.Date >= start.AddMonths(-1) && t.Date < start)
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => -g.Sum(t => t.AmountGr));
        return transactions.Where(t => t.AmountGr < 0 && t.Date >= start && t.Date <= end)
            .GroupBy(t => t.Category)
            .Select(g =>
            {
                var amount = -g.Sum(t => t.AmountGr);
                var previous = before.GetValueOrDefault(g.Key);
                decimal? change = previous == 0 ? null : decimal.Round((amount - previous) * 100m / previous, 1, MidpointRounding.AwayFromZero);
                return (g.Key, amount, change, (IReadOnlyList<Guid>)Ids(g));
            })
            .OrderByDescending(c => c.amount)
            .ThenBy(c => c.Key)
            .ToList();
    }

    private static List<CategoryShare> TopCategories(List<Transaction> expenses, long totalSpent) => expenses
        .GroupBy(t => t.Category)
        .Select(g => (Category: g.Key, Amount: -g.Sum(t => t.AmountGr)))
        .OrderByDescending(c => c.Amount)
        .ThenBy(c => c.Category)
        .Take(3)
        .Select(c => new CategoryShare(c.Category, c.Amount, totalSpent == 0 ? 0 : Pct(c.Amount, totalSpent)))
        .ToList();

    /// <summary>The most visited merchant among discretionary spending (rent, bills and transfers excluded).</summary>
    private static MerchantStat? TopMerchant(List<Transaction> expenses) => expenses
        .Where(t => !NonDiscretionary.Contains(t.Category))
        .GroupBy(t => t.Merchant)
        .Select(g => new MerchantStat(g.Key, g.Count(), -g.Sum(t => t.AmountGr), Ids(g)))
        .OrderByDescending(m => m.Count)
        .ThenByDescending(m => m.AmountGr)
        .ThenBy(m => m.Name, StringComparer.Ordinal)
        .FirstOrDefault();

    /// <summary>The day with the most spontaneous spending (recurring payments and BNPL instalments excluded).</summary>
    private static DayStat? BiggestDay(List<Transaction> expenses) => expenses
        .Where(t => !t.IsRecurring && !t.IsBnpl)
        .GroupBy(t => t.Date)
        .Select(g => new DayStat(g.Key, -g.Sum(t => t.AmountGr), g.Count(), Ids(g)))
        .OrderByDescending(d => d.AmountGr)
        .ThenBy(d => d.Date)
        .FirstOrDefault();

    /// <summary>The category with the largest absolute change against last month, among categories over 50 zł last month.</summary>
    private static CategoryChange? BiggestChange(List<Transaction> expenses, List<Transaction> previous)
    {
        var before = previous.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => -g.Sum(t => t.AmountGr));
        return expenses
            .GroupBy(t => t.Category)
            .Select(g => (Category: g.Key, To: -g.Sum(t => t.AmountGr), From: before.GetValueOrDefault(g.Key)))
            .Where(c => c.From >= 5_000 && !NonDiscretionary.Contains(c.Category))
            .Where(c => c.To != c.From)
            .OrderByDescending(c => Math.Abs(c.To - c.From))
            .ThenBy(c => c.Category)
            .Select(c => new CategoryChange(c.Category, c.From, c.To, Pct(c.To - c.From, c.From)))
            .FirstOrDefault();
    }

    private static DayOfWeek CheapestWeekday(List<Transaction> expenses, DateOnly start, DateOnly end)
    {
        var occurrences = new Dictionary<DayOfWeek, int>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            occurrences[day.DayOfWeek] = occurrences.GetValueOrDefault(day.DayOfWeek) + 1;
        }

        var spent = expenses
            .Where(t => !t.IsRecurring && !t.IsBnpl)
            .GroupBy(t => t.Date.DayOfWeek)
            .ToDictionary(g => g.Key, g => -g.Sum(t => t.AmountGr));

        // Monday first, so ties go to the earlier weekday.
        return occurrences.Keys
            .OrderBy(day => (decimal)spent.GetValueOrDefault(day) / occurrences[day])
            .ThenBy(day => ((int)day + 6) % 7)
            .First();
    }

    public static Personality PersonalityOf(IReadOnlyCollection<Transaction> expenses, long totalSpent)
    {
        if (totalSpent == 0)
        {
            return new Personality("steady_planner", "🧭");
        }

        decimal Share(Func<Transaction, bool> predicate) => -expenses.Where(predicate).Sum(t => t.AmountGr) / (decimal)totalSpent;

        var discretionary = expenses.Where(t => !t.IsRecurring && !t.IsBnpl && !NonDiscretionary.Contains(t.Category)).ToList();
        var weekend = discretionary.Count == 0 ? 0 : -discretionary.Where(t => t.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).Sum(t => t.AmountGr)
            / (decimal)-discretionary.Sum(t => t.AmountGr);

        return expenses.Count(t => t.IsBnpl) >= 3 ? new Personality("instalment_juggler", "🤹")
            : Share(t => t.Category == Category.FoodDelivery) >= 0.10m ? new Personality("delivery_lover", "🛵")
            : Share(OpportunityFinder.IsTaxi) >= 0.06m ? new Personality("city_rider", "🚕")
            : Share(t => t.Category == Category.Shopping) >= 0.15m ? new Personality("bargain_hunter", "🛍️")
            : weekend >= 0.40m ? new Personality("weekend_foodie", "🌙")
            : new Personality("steady_planner", "🧭");
    }

    private static int Pct(long part, long whole) => (int)Math.Round(part * 100m / whole, MidpointRounding.AwayFromZero);

    private static List<Guid> Ids(IEnumerable<Transaction> transactions) =>
        transactions.OrderByDescending(t => t.Date).Select(t => t.Id).ToList();
}

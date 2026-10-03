using CashCoach.Core.Domain;

namespace CashCoach.Core.Services;

public enum SummaryPeriod
{
    ThisMonth,
    LastMonth,
    LastThreeMonths,
}

public sealed record DateRange(DateOnly From, DateOnly To)
{
    public bool Contains(DateOnly date) => date >= From && date <= To;
}

public static class SummaryPeriods
{
    public static bool TryParse(string? value, out SummaryPeriod period)
    {
        (var parsed, period) = value switch
        {
            "this_month" => (true, SummaryPeriod.ThisMonth),
            "last_month" => (true, SummaryPeriod.LastMonth),
            "last_3_months" => (true, SummaryPeriod.LastThreeMonths),
            _ => (false, default),
        };
        return parsed;
    }

    /// <summary>Calendar months relative to <paramref name="referenceDate"/>, plus the equally long range just before them.</summary>
    public static (DateRange Current, DateRange Previous) Resolve(SummaryPeriod period, DateOnly referenceDate)
    {
        var referenceMonth = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
        var (start, months) = period switch
        {
            SummaryPeriod.ThisMonth => (referenceMonth, 1),
            SummaryPeriod.LastMonth => (referenceMonth.AddMonths(-1), 1),
            SummaryPeriod.LastThreeMonths => (referenceMonth.AddMonths(-2), 3),
            _ => throw new ArgumentOutOfRangeException(nameof(period), period, null),
        };

        return (Months(start, months), Months(start.AddMonths(-months), months));
    }

    private static DateRange Months(DateOnly start, int count) => new(start, start.AddMonths(count).AddDays(-1));
}

/// <param name="AmountGr">Positive amount spent in grosze.</param>
public sealed record MerchantSpending(string Merchant, long AmountGr, int Count);

/// <param name="AmountGr">Positive amount spent in grosze.</param>
/// <param name="Share">Fraction of the period's total spending, 0 to 1.</param>
/// <param name="VsPrevPct">Change against the previous period in percent, or <c>null</c> when nothing was spent then.</param>
public sealed record CategorySpending(
    Category Category,
    long AmountGr,
    int Count,
    decimal Share,
    decimal? VsPrevPct,
    IReadOnlyList<MerchantSpending> TopMerchants);

/// <param name="TotalSpentGr">Positive total of all expenses in the period, in grosze.</param>
public sealed record SpendingSummary(DateRange Period, long TotalSpentGr, IReadOnlyList<CategorySpending> Categories);

public static class SpendingSummaryCalculator
{
    public const int TopMerchantCount = 3;

    public static SpendingSummary Summarize(IEnumerable<Transaction> transactions, SummaryPeriod period, DateOnly referenceDate)
    {
        var (current, previous) = SummaryPeriods.Resolve(period, referenceDate);
        var expenses = transactions.Where(transaction => transaction.AmountGr < 0).ToList();
        var currentExpenses = expenses.Where(transaction => current.Contains(transaction.Date)).ToList();
        var previousByCategory = expenses
            .Where(transaction => previous.Contains(transaction.Date))
            .GroupBy(transaction => transaction.Category)
            .ToDictionary(group => group.Key, group => -group.Sum(transaction => transaction.AmountGr));

        var totalSpentGr = -currentExpenses.Sum(transaction => transaction.AmountGr);
        var categories = currentExpenses
            .GroupBy(transaction => transaction.Category)
            .Select(group =>
            {
                var amountGr = -group.Sum(transaction => transaction.AmountGr);
                return new CategorySpending(
                    group.Key,
                    amountGr,
                    group.Count(),
                    decimal.Round((decimal)amountGr / totalSpentGr, 4, MidpointRounding.AwayFromZero),
                    PercentChange(amountGr, previousByCategory.GetValueOrDefault(group.Key)),
                    TopMerchants(group));
            })
            .OrderByDescending(category => category.AmountGr)
            .ThenBy(category => category.Category)
            .ToList();

        return new SpendingSummary(current, totalSpentGr, categories);
    }

    private static decimal? PercentChange(long currentGr, long previousGr) => previousGr == 0
        ? null
        : decimal.Round((decimal)(currentGr - previousGr) * 100 / previousGr, 1, MidpointRounding.AwayFromZero);

    private static List<MerchantSpending> TopMerchants(IEnumerable<Transaction> transactions) => transactions
        .GroupBy(transaction => transaction.Merchant)
        .Select(group => new MerchantSpending(group.Key, -group.Sum(transaction => transaction.AmountGr), group.Count()))
        .OrderByDescending(merchant => merchant.AmountGr)
        .ThenBy(merchant => merchant.Merchant, StringComparer.Ordinal)
        .Take(TopMerchantCount)
        .ToList();
}

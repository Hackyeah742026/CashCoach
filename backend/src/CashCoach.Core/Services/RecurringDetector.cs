using CashCoach.Core.Domain;

namespace CashCoach.Core.Services;

/// <param name="Type">The recurring type, or <c>null</c> for a regular payment that is none of subscription, rent, salary or BNPL (e.g. a phone bill).</param>
/// <param name="AvgAmountGr">Signed average in grosze: negative for payments, positive for income.</param>
public sealed record DetectedRecurring(
    string Merchant,
    RecurringType? Type,
    long AvgAmountGr,
    int PeriodDays,
    DateOnly? NextDate,
    bool Active,
    IReadOnlyList<Guid> TransactionIds);

/// <param name="SalaryDay">Usual day of month of the salary, or <c>null</c> when no regular income was found.</param>
public sealed record RecurringDetectionResult(IReadOnlyList<DetectedRecurring> Groups, int? SalaryDay);

/// <summary>Finds subscriptions, rent, salary and BNPL plans in a user's transactions.</summary>
public static class RecurringDetector
{
    public const int MonthlyPeriodDays = 30;
    public const int WeeklyPeriodDays = 7;
    private const decimal MaxAmountVariation = 0.10m;

    /// <summary>A series whose last payment is older than one period plus this many days is treated as cancelled.</summary>
    private const int InactiveGraceDays = 10;

    public static bool IsMonthlyInterval(int days) => days is >= 28 and <= 33;

    public static bool IsWeeklyInterval(int days) => days is >= 6 and <= 8;

    public static RecurringDetectionResult Detect(IReadOnlyCollection<Transaction> transactions)
    {
        if (transactions.Count == 0)
        {
            return new RecurringDetectionResult([], null);
        }

        var referenceDate = transactions.Max(transaction => transaction.Date);
        var groups = new List<DetectedRecurring>();

        groups.AddRange(BnplPlanBuilder.Build(transactions).Select(plan => new DetectedRecurring(
            plan.Merchant, RecurringType.Bnpl, -plan.InstalmentGr, MonthlyPeriodDays, plan.NextDate, plan.Active, plan.TransactionIds)));

        var outgoing = FindSeries(transactions.Where(t => t.AmountGr < 0 && !t.IsBnpl), referenceDate);
        var rent = FindRent(outgoing);
        groups.AddRange(outgoing.Select(series => series.ToDetected(
            IsSubscriptionCategory(series.Category) ? RecurringType.Subscription
            : series == rent ? RecurringType.Rent
            : null)));

        var incoming = FindSeries(transactions.Where(t => t.AmountGr > 0), referenceDate);
        var salary = incoming.Where(series => series.PeriodDays == MonthlyPeriodDays).MaxBy(series => series.AvgAmountGr);
        groups.AddRange(incoming.Select(series => series.ToDetected(series == salary ? RecurringType.Salary : null)));

        return new RecurringDetectionResult(groups, salary?.PayDay);
    }

    private static bool IsSubscriptionCategory(Category category) =>
        category is Category.Subscriptions or Category.Entertainment;

    /// <summary>The largest monthly outflow, preferring payments categorized as rent over generic transfers.</summary>
    private static Series? FindRent(IReadOnlyList<Series> outgoing)
    {
        var monthly = outgoing
            .Where(series => series.PeriodDays == MonthlyPeriodDays && !IsSubscriptionCategory(series.Category))
            .ToList();
        return monthly.Where(series => series.Category == Category.Rent).MinBy(series => series.AvgAmountGr)
            ?? monthly.Where(series => series.Category is Category.Transfers or Category.Other).MinBy(series => series.AvgAmountGr);
    }

    private static List<Series> FindSeries(IEnumerable<Transaction> transactions, DateOnly referenceDate) => transactions
        .GroupBy(transaction => transaction.Merchant, StringComparer.OrdinalIgnoreCase)
        .Select(group => Series.TryCreate(group.OrderBy(transaction => transaction.Date).ToList(), referenceDate))
        .OfType<Series>()
        .ToList();

    private sealed record Series(
        string Merchant,
        Category Category,
        IReadOnlyList<Transaction> Transactions,
        int PeriodDays,
        long AvgAmountGr,
        DateOnly NextDate,
        bool Active)
    {
        /// <summary>Most common day of month; ties go to the later day.</summary>
        public int PayDay => Transactions
            .GroupBy(transaction => transaction.Date.Day)
            .OrderByDescending(group => group.Count())
            .ThenByDescending(group => group.Key)
            .First().Key;

        public static Series? TryCreate(IReadOnlyList<Transaction> ordered, DateOnly referenceDate)
        {
            if (ordered.Count < 2)
            {
                return null;
            }

            var intervals = ordered.Zip(ordered.Skip(1), (previous, next) => next.Date.DayNumber - previous.Date.DayNumber).ToList();
            int? periodDays = intervals.All(IsMonthlyInterval) ? MonthlyPeriodDays
                : intervals.All(IsWeeklyInterval) ? WeeklyPeriodDays
                : null;
            if (periodDays is null)
            {
                return null;
            }

            var magnitudes = ordered.Select(transaction => (decimal)Math.Abs(transaction.AmountGr)).ToList();
            var average = magnitudes.Average();
            if (average == 0 || (magnitudes.Max() - magnitudes.Min()) / average > MaxAmountVariation)
            {
                return null;
            }

            var last = ordered[^1];
            var nextDate = periodDays == MonthlyPeriodDays ? last.Date.AddMonths(1) : last.Date.AddDays(WeeklyPeriodDays);
            var active = referenceDate.DayNumber - last.Date.DayNumber <= periodDays.Value + InactiveGraceDays;

            return new Series(
                last.Merchant,
                last.Category,
                ordered,
                periodDays.Value,
                Money.Round(ordered.Average(transaction => (decimal)transaction.AmountGr)),
                nextDate,
                active);
        }

        public DetectedRecurring ToDetected(RecurringType? type)
        {
            var nextDate = type == RecurringType.Salary ? NextPayDay() : NextDate;
            return new DetectedRecurring(Merchant, type, AvgAmountGr, PeriodDays, nextDate, Active,
                Transactions.Select(transaction => transaction.Id).ToList());
        }

        private DateOnly NextPayDay()
        {
            var month = Transactions[^1].Date.AddMonths(1);
            return new DateOnly(month.Year, month.Month, Math.Min(PayDay, DateTime.DaysInMonth(month.Year, month.Month)));
        }
    }
}

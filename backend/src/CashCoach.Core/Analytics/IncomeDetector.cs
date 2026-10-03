using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

public enum IncomeKind
{
    Salary,
    Stipend,
    Other,
}

public enum DetectionConfidence
{
    High,
    Medium,
    Low,
}

/// <param name="Source">Display name of the payer or title, e.g. <c>Wynagrodzenie</c>.</param>
/// <param name="Day">Usual day of month (31 for <see cref="PaydayRule.LastWorkingDay"/>).</param>
/// <param name="AmountGr">Median monthly amount in grosze.</param>
/// <param name="TransactionIds">The matched payments, newest first.</param>
public sealed record IncomeCandidate(
    string Source,
    IncomeKind Kind,
    int Day,
    PaydayRule DayRule,
    long AmountGr,
    long MinGr,
    long MaxGr,
    int MonthsSeen,
    DetectionConfidence Confidence,
    IReadOnlyList<Guid> TransactionIds);

/// <summary>
/// Finds regular monthly income (salary, stipend…) in a transaction history, tolerating weekend shifts,
/// a few days of jitter and changing amounts (bonuses). Candidates are ranked largest first.
/// </summary>
public static class IncomeDetector
{
    public const int MinMonths = 2;
    public const int NearDays = 3;
    public const decimal AmountTolerance = 0.25m;
    public const decimal StableAmountTolerance = 0.05m;
    public const long MinAmountGr = 5_000;

    private static readonly string[] SalaryKeywords = ["WYNAGRODZENIE", "PENSJA", "WYPLATA", "WYPŁATA", "SALARY", "PAYROLL"];
    private static readonly string[] StipendKeywords = ["STYPENDIUM", "SCHOLARSHIP"];

    public static IReadOnlyList<IncomeCandidate> Detect(IEnumerable<Transaction> transactions) => transactions
        .Where(t => t.AmountGr >= MinAmountGr)
        .GroupBy(GroupKey)
        .Select(group => Analyze(group.ToList()))
        .OfType<IncomeCandidate>()
        .OrderByDescending(c => c.AmountGr)
        .ThenBy(c => c.Confidence)
        .ToList();

    private static string GroupKey(Transaction t) => KindOf(t) switch
    {
        IncomeKind.Salary => "#salary",
        IncomeKind.Stipend => "#stipend",
        _ => t.Merchant.ToUpperInvariant(),
    };

    private static IncomeKind KindOf(Transaction t)
    {
        var text = $"{t.RawDescription} {t.Merchant}".ToUpperInvariant();
        return StipendKeywords.Any(text.Contains) ? IncomeKind.Stipend
            : SalaryKeywords.Any(text.Contains) || t.Category == Category.Salary ? IncomeKind.Salary
            : IncomeKind.Other;
    }

    private static IncomeCandidate? Analyze(List<Transaction> group)
    {
        // One payment per month: the largest (a second smaller one is usually a refund or bonus split).
        var monthly = group
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .Select(g => g.OrderByDescending(t => t.AmountGr).First())
            .OrderBy(t => t.Date)
            .ToList();
        if (monthly.Count < MinMonths || !HasConsecutiveMonths(monthly))
        {
            return null;
        }

        var (day, rule, matches) = BestDay(monthly);
        if (day is null)
        {
            return null;
        }

        var amounts = monthly.Select(t => t.AmountGr).Order().ToList();
        var median = Median(amounts);
        var within = amounts.Count(a => Math.Abs(a - median) <= median * AmountTolerance);
        var allowedOutliers = monthly.Count >= 3 ? 1 : 0;
        if (within < monthly.Count - allowedOutliers)
        {
            return null;
        }

        var stable = amounts.All(a => Math.Abs(a - median) <= median * StableAmountTolerance);
        var allMatch = matches == monthly.Count;
        var confidence = monthly.Count >= 3 && allMatch && stable ? DetectionConfidence.High
            : monthly.Count >= 3 || (allMatch && stable) ? DetectionConfidence.Medium
            : DetectionConfidence.Low;

        return new IncomeCandidate(
            monthly[^1].Merchant,
            KindOf(monthly[^1]),
            day.Value,
            rule,
            median,
            amounts[0],
            amounts[^1],
            monthly.Count,
            confidence,
            monthly.OrderByDescending(t => t.Date).Select(t => t.Id).ToList());
    }

    private static bool HasConsecutiveMonths(List<Transaction> monthly) => monthly
        .Zip(monthly.Skip(1), (a, b) => (b.Date.Year * 12 + b.Date.Month) - (a.Date.Year * 12 + a.Date.Month))
        .Any(gap => gap == 1);

    /// <returns>The usual day, its rule and how many payments fall exactly on it; <c>null</c> when the payments are not regular.</returns>
    private static (int? Day, PaydayRule Rule, int Matches) BestDay(List<Transaction> monthly)
    {
        var lastWorkingDay = monthly.Count(t => t.Date == LastWorkingDay(t.Date.Year, t.Date.Month));
        if (lastWorkingDay == monthly.Count && monthly.Select(t => t.Date.Day).Distinct().Count() > 1)
        {
            return (31, PaydayRule.LastWorkingDay, lastWorkingDay);
        }

        var best = Enumerable.Range(1, 31)
            .Select(day => (Day: day, Exact: monthly.Count(t => Matches(t.Date, day)), Near: monthly.Count(t => IsNear(t.Date, day))))
            .OrderByDescending(c => c.Exact)
            .ThenByDescending(c => c.Near)
            .ThenByDescending(c => c.Day)
            .First();

        return best.Near == monthly.Count ? (best.Day, PaydayRule.FixedDay, best.Exact) : (null, PaydayRule.FixedDay, 0);
    }

    /// <summary>Paid on the day itself, or on the Friday before when the day falls on a weekend.</summary>
    private static bool Matches(DateOnly date, int day)
    {
        var nominal = OnDay(date.Year, date.Month, day);
        return date == nominal || date == PreviousWorkingDay(nominal);
    }

    private static bool IsNear(DateOnly date, int day) =>
        Math.Abs(date.DayNumber - OnDay(date.Year, date.Month, day).DayNumber) <= NearDays;

    public static DateOnly LastWorkingDay(int year, int month) =>
        PreviousWorkingDay(new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

    private static DateOnly PreviousWorkingDay(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    private static DateOnly OnDay(int year, int month, int day) => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static long Median(List<long> sorted) =>
        sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : Money.Round((sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2m);
}

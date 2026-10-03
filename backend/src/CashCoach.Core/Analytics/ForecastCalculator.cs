using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

public enum ForecastStatus
{
    Ok,
    Tight,
    Danger,
}

/// <param name="AmountGr">Positive amount of the payment in grosze.</param>
public sealed record UpcomingPayment(DateOnly Date, string Merchant, RecurringType Type, long AmountGr, Guid? GroupId);

public sealed record BalancePoint(DateOnly Date, long BalanceGr);

/// <param name="DaysLeft">Days from <paramref name="AsOf"/> to <paramref name="NextPayday"/>.</param>
/// <param name="FixedUpcomingGr">Positive total of recurring payments due after <paramref name="AsOf"/> and before payday.</param>
/// <param name="DailyVariableGr">Median daily non-recurring spending over the last 30 days, positive.</param>
/// <param name="ProjectedEndGr">Balance expected on payday before the salary: balance − fixed − daily × days left.</param>
/// <param name="RunOutDate">First day the simulated balance goes below zero, or <c>null</c>.</param>
/// <param name="SafeToSpendGr">Balance − fixed upcoming − safety buffer; negative when the bills and buffer do not fit.</param>
/// <param name="SafePerDayGr">Non-negative safe-to-spend divided by days left.</param>
/// <param name="Series">Simulated end-of-day balance from <paramref name="AsOf"/> to payday.</param>
public sealed record Forecast(
    DateOnly AsOf,
    DateOnly NextPayday,
    int DaysLeft,
    long BalanceGr,
    long FixedUpcomingGr,
    IReadOnlyList<UpcomingPayment> Upcoming,
    long DailyVariableGr,
    long ProjectedEndGr,
    DateOnly? RunOutDate,
    ForecastStatus Status,
    long SafetyBufferGr,
    long SafeToSpendGr,
    long SafePerDayGr,
    IReadOnlyList<BalancePoint> Series,
    IReadOnlyList<Guid> UpcomingTransactionIds);

/// <summary>Projects the balance day by day until the next payday.</summary>
public static class ForecastCalculator
{
    /// <summary>A projected end balance below this (but not negative) is <see cref="ForecastStatus.Tight"/>.</summary>
    public const long TightThresholdGr = 20_000;

    public const int VariableWindowDays = 30;

    private static readonly RecurringType[] PaymentTypes = [RecurringType.Subscription, RecurringType.Rent, RecurringType.Bnpl];

    /// <param name="extraPayments">One-off payments to add (e.g. a simulated purchase), positive grosze. Dates on or after payday land on payday.</param>
    public static Forecast Compute(FinancialSnapshot snapshot, IReadOnlyList<UpcomingPayment>? extraPayments = null)
    {
        var asOf = snapshot.AsOf;
        var nextPayday = NextPayday(asOf, snapshot.Payday);
        var daysLeft = nextPayday.DayNumber - asOf.DayNumber;

        var upcoming = UpcomingPayments(snapshot.Recurring, asOf, nextPayday);
        var fixedGr = upcoming.Sum(p => p.AmountGr);
        var extras = (extraPayments ?? [])
            .Select(p => p with { Date = Clamp(p.Date, asOf.AddDays(1), nextPayday) })
            .ToList();
        var dailyGr = DailyVariable(snapshot);

        var balance = snapshot.BalanceGr;
        DateOnly? runOut = balance < 0 ? asOf : null;
        var series = new List<BalancePoint> { new(asOf, balance) };
        for (var day = asOf.AddDays(1); day <= nextPayday; day = day.AddDays(1))
        {
            balance -= dailyGr
                + upcoming.Where(p => p.Date == day).Sum(p => p.AmountGr)
                + extras.Where(p => p.Date == day).Sum(p => p.AmountGr);
            series.Add(new BalancePoint(day, balance));
            if (balance < 0 && runOut is null)
            {
                runOut = day;
            }
        }

        var projectedEnd = balance;
        var safeToSpend = snapshot.BalanceGr - fixedGr - extras.Sum(p => p.AmountGr) - snapshot.SafetyBufferGr;
        var upcomingGroupIds = upcoming.Select(p => p.GroupId).OfType<Guid>().ToHashSet();

        return new Forecast(
            asOf,
            nextPayday,
            daysLeft,
            snapshot.BalanceGr,
            fixedGr,
            upcoming,
            dailyGr,
            projectedEnd,
            runOut,
            StatusOf(projectedEnd),
            snapshot.SafetyBufferGr,
            safeToSpend,
            Math.Max(0, safeToSpend) / daysLeft,
            series,
            snapshot.Transactions
                .Where(t => t.RecurringGroupId is { } id && upcomingGroupIds.Contains(id))
                .OrderByDescending(t => t.Date)
                .Select(t => t.Id)
                .ToList());
    }

    public static ForecastStatus StatusOf(long projectedEndGr) =>
        projectedEndGr >= TightThresholdGr ? ForecastStatus.Ok
        : projectedEndGr >= 0 ? ForecastStatus.Tight
        : ForecastStatus.Danger;

    /// <summary>The first payday strictly after <paramref name="asOf"/>; the first of next month when the payday is unknown.</summary>
    public static DateOnly NextPayday(DateOnly asOf, int? payday)
    {
        if (payday is not { } day)
        {
            return new DateOnly(asOf.Year, asOf.Month, 1).AddMonths(1);
        }

        var thisMonth = OnDay(asOf.Year, asOf.Month, day);
        if (thisMonth > asOf)
        {
            return thisMonth;
        }

        var next = asOf.AddMonths(1);
        return OnDay(next.Year, next.Month, day);
    }

    /// <summary>The payday that started the current pay period (one month before <see cref="NextPayday"/>).</summary>
    public static DateOnly PreviousPayday(DateOnly asOf, int? payday)
    {
        var previous = NextPayday(asOf, payday).AddMonths(-1);
        return payday is { } day ? OnDay(previous.Year, previous.Month, day) : previous;
    }

    /// <summary>Recurring payments due after <paramref name="asOf"/> and before <paramref name="until"/>. BNPL plans contribute only their next instalment.</summary>
    public static List<UpcomingPayment> UpcomingPayments(IEnumerable<RecurringGroup> recurring, DateOnly asOf, DateOnly until)
    {
        var payments = new List<UpcomingPayment>();
        foreach (var group in recurring.Where(g => g.Active && g.AvgAmountGr < 0 && g.NextDate is not null && PaymentTypes.Contains(g.Type)))
        {
            var date = group.NextDate!.Value;
            while (date <= asOf)
            {
                date = Advance(date, group.PeriodDays);
            }

            while (date < until)
            {
                payments.Add(new UpcomingPayment(date, group.Merchant, group.Type, -group.AvgAmountGr, group.Id));
                if (group.Type == RecurringType.Bnpl)
                {
                    break;
                }

                date = Advance(date, group.PeriodDays);
            }
        }

        return payments.OrderBy(p => p.Date).ThenBy(p => p.Merchant, StringComparer.Ordinal).ToList();
    }

    /// <summary>Median of daily non-recurring, non-BNPL spending over the last 30 days (days without spending count as zero).</summary>
    public static long DailyVariable(FinancialSnapshot snapshot)
    {
        var window = snapshot.RecentWindow(VariableWindowDays);
        var byDay = snapshot.ExpensesBetween(window.From, window.To)
            .Where(t => !t.IsRecurring && !t.IsBnpl)
            .GroupBy(t => t.Date)
            .ToDictionary(g => g.Key, g => -g.Sum(t => t.AmountGr));
        var daily = Enumerable.Range(0, window.Days)
            .Select(offset => byDay.GetValueOrDefault(window.From.AddDays(offset)))
            .Order()
            .ToList();

        var middle = daily.Count / 2;
        return daily.Count % 2 == 1 ? daily[middle] : Money.Round((daily[middle - 1] + daily[middle]) / 2m);
    }

    private static DateOnly Advance(DateOnly date, int periodDays) =>
        periodDays == Services.RecurringDetector.WeeklyPeriodDays ? date.AddDays(periodDays) : date.AddMonths(1);

    private static DateOnly OnDay(int year, int month, int day) => new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static DateOnly Clamp(DateOnly date, DateOnly min, DateOnly max) => date < min ? min : date > max ? max : date;
}

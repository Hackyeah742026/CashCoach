using CashCoach.Core.Domain;

namespace CashCoach.Core.Services;

/// <param name="InstalmentGr">Average instalment as a positive amount in grosze.</param>
/// <param name="Paid">Highest instalment number seen, e.g. 2 for <c>RATA 2/4</c>.</param>
public sealed record BnplPlan(
    string Provider,
    string Merchant,
    long InstalmentGr,
    int Paid,
    int Total,
    DateOnly LastPaymentDate,
    DateOnly? NextDate,
    IReadOnlyList<Guid> TransactionIds)
{
    public int RemainingInstalments => Total - Paid;
    public long RemainingGr => RemainingInstalments * InstalmentGr;
    public bool Active => Paid < Total;
}

public sealed record BnplSummary(IReadOnlyList<BnplPlan> Plans)
{
    public int ActivePlans => Plans.Count(plan => plan.Active);
    public long TotalRemainingGr => Plans.Where(plan => plan.Active).Sum(plan => plan.RemainingGr);
}

/// <summary>Rebuilds BNPL plans from instalment payments marked <c>n/m</c> (e.g. <c>KLARNA*ZALANDO RATA 2/4</c>).</summary>
public static class BnplPlanBuilder
{
    public const string UnknownProvider = "other";
    private const int DefaultIntervalDays = 30;

    public static IReadOnlyList<BnplPlan> Build(IEnumerable<Transaction> transactions) => transactions
        .Where(transaction => transaction.IsBnpl && transaction.AmountGr < 0)
        .Select(transaction => (Transaction: transaction, Normalized: MerchantNormalizer.Normalize(transaction.RawDescription)))
        .Where(item => item.Normalized.InstalmentCount is not null)
        .GroupBy(item => (
            Provider: item.Normalized.BnplProvider ?? UnknownProvider,
            item.Transaction.Merchant,
            Total: item.Normalized.InstalmentCount!.Value))
        .Select(group => BuildPlan(group.Key.Provider, group.Key.Merchant, group.Key.Total, group.ToList()))
        .OrderBy(plan => plan.NextDate ?? DateOnly.MaxValue)
        .ThenBy(plan => plan.Merchant, StringComparer.Ordinal)
        .ToList();

    private static BnplPlan BuildPlan(
        string provider, string merchant, int total, List<(Transaction Transaction, NormalizedDescription Normalized)> instalments)
    {
        var ordered = instalments.OrderBy(item => item.Transaction.Date).ToList();
        var first = ordered[0].Transaction.Date;
        var last = ordered[^1].Transaction.Date;
        var paid = ordered.Max(item => item.Normalized.InstalmentNumber!.Value);
        var instalmentGr = Money.Round(ordered.Average(item => (decimal)-item.Transaction.AmountGr));

        DateOnly? nextDate = null;
        if (paid < total)
        {
            var intervalDays = ordered.Count > 1
                ? (int)Math.Round((last.DayNumber - first.DayNumber) / (double)(ordered.Count - 1))
                : DefaultIntervalDays;
            nextDate = RecurringDetector.IsMonthlyInterval(intervalDays) ? last.AddMonths(1) : last.AddDays(intervalDays);
        }

        return new BnplPlan(
            provider, merchant, instalmentGr, paid, total, last, nextDate,
            ordered.Select(item => item.Transaction.Id).ToList());
    }
}

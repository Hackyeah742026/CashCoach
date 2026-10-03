using CashCoach.Core.Domain;
using CashCoach.Core.Services;

namespace CashCoach.Core.Analytics;

public enum OpportunityType
{
    FoodDelivery,
    DuplicateSubscription,
    UnusedSubscription,
    SmallDailyBuys,
    TaxiRides,
    Bnpl,
}

public enum Difficulty
{
    Easy,
    Medium,
    Hard,
}

/// <param name="Id">Stable across requests (e.g. <c>food_delivery</c>, <c>duplicate_subscription:music</c>), so it can be dismissed.</param>
/// <param name="Subject">What the opportunity is about: a merchant list or subscription group, for display.</param>
/// <param name="MonthlySpendGr">Current monthly spending the saving comes from, positive.</param>
/// <param name="Count">Payments per month behind <paramref name="MonthlySpendGr"/> (rounded).</param>
public sealed record Opportunity(
    string Id,
    OpportunityType Type,
    string Subject,
    long MonthlySavingGr,
    long MonthlySpendGr,
    int Count,
    Difficulty Difficulty,
    IReadOnlyList<Guid> TransactionIds)
{
    public string TitleKey => $"opportunity.{SnakeCaseEnum<OpportunityType>.ToName(Type)}";
    public long YearlySavingGr => MonthlySavingGr * 12;
}

/// <summary>Rule-based savings opportunities. Monthly figures are averaged over the last 90 days of history.</summary>
public static class OpportunityFinder
{
    public const int Top = 5;
    public const int WindowDays = 90;

    public const long MinDeliveryMonthlyGr = 10_000;
    public const long SmallBuyMaxGr = 2_500;
    public const int MinSmallBuysPerMonth = 12;
    public const long MinTaxiMonthlyGr = 6_000;

    private static readonly string[] TaxiMerchants = ["bolt", "uber", "free now", "freenow", "itaxi"];

    /// <summary>All opportunities, largest monthly saving first. Callers drop dismissed ones and take <see cref="Top"/>.</summary>
    public static IReadOnlyList<Opportunity> Find(FinancialSnapshot snapshot)
    {
        var window = snapshot.RecentWindow(WindowDays);
        var expenses = snapshot.ExpensesBetween(window.From, window.To).ToList();
        var opportunities = new List<Opportunity>();

        var delivery = expenses.Where(t => t.Category == Category.FoodDelivery).ToList();
        var deliveryMonthly = Monthly(delivery, window);
        if (deliveryMonthly >= MinDeliveryMonthlyGr)
        {
            opportunities.Add(new Opportunity(
                "food_delivery", OpportunityType.FoodDelivery, TopMerchants(delivery), deliveryMonthly / 2, deliveryMonthly,
                MonthlyCount(delivery.Count, window), Difficulty.Medium, Ids(delivery)));
        }

        opportunities.AddRange(SubscriptionOpportunities(snapshot));

        var smallBuys = expenses
            .Where(t => t.Category is Category.Groceries or Category.Restaurants && -t.AmountGr <= SmallBuyMaxGr && !t.IsRecurring)
            .ToList();
        var smallCount = MonthlyCount(smallBuys.Count, window);
        if (smallCount >= MinSmallBuysPerMonth)
        {
            var monthly = Monthly(smallBuys, window);
            opportunities.Add(new Opportunity(
                "small_daily_buys", OpportunityType.SmallDailyBuys, TopMerchants(smallBuys), Money.Round(monthly * 0.3m), monthly,
                smallCount, Difficulty.Medium, Ids(smallBuys)));
        }

        var taxi = expenses.Where(IsTaxi).ToList();
        var taxiMonthly = Monthly(taxi, window);
        if (taxiMonthly >= MinTaxiMonthlyGr)
        {
            opportunities.Add(new Opportunity(
                "taxi_rides", OpportunityType.TaxiRides, TopMerchants(taxi), taxiMonthly / 2, taxiMonthly,
                MonthlyCount(taxi.Count, window), Difficulty.Medium, Ids(taxi)));
        }

        var activePlans = BnplPlanBuilder.Build(snapshot.Transactions).Where(plan => plan.Active).ToList();
        if (activePlans.Count > 0)
        {
            var instalments = activePlans.Sum(plan => plan.InstalmentGr);
            opportunities.Add(new Opportunity(
                "bnpl", OpportunityType.Bnpl, string.Join(", ", activePlans.Select(plan => plan.Merchant)), instalments, instalments,
                activePlans.Count, Difficulty.Hard, activePlans.SelectMany(plan => plan.TransactionIds).ToList()));
        }

        return opportunities
            .Where(o => o.MonthlySavingGr > 0)
            .OrderByDescending(o => o.MonthlySavingGr)
            .ThenBy(o => o.Id, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsTaxi(Transaction transaction) =>
        transaction.Category == Category.Transport
        && TaxiMerchants.Any(name => transaction.Merchant.Contains(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Subscriptions the user said they no longer use, then overlapping ones (keeping the most expensive of each group).</summary>
    private static IEnumerable<Opportunity> SubscriptionOpportunities(FinancialSnapshot snapshot)
    {
        var subscriptions = snapshot.Recurring.Where(g => g.Active && g.Type == RecurringType.Subscription).ToList();
        foreach (var unused in subscriptions.Where(s => s.UserConfirmed == false))
        {
            var monthly = MonthlyCost(unused);
            yield return new Opportunity(
                $"unused_subscription:{unused.Id}", OpportunityType.UnusedSubscription, unused.Merchant, monthly, monthly, 1,
                Difficulty.Easy, GroupTransactionIds(snapshot, [unused]));
        }

        var groups = subscriptions
            .Where(s => s.UserConfirmed != false)
            .Select(s => (Subscription: s, Group: SubscriptionOverviewCalculator.GroupOf(s.Merchant)))
            .Where(item => item.Group is not null)
            .GroupBy(item => item.Group!)
            .Where(group => group.Count() > 1);
        foreach (var group in groups)
        {
            var members = group.Select(item => item.Subscription).OrderByDescending(MonthlyCost).ThenBy(s => s.Merchant, StringComparer.Ordinal).ToList();
            var total = members.Sum(MonthlyCost);
            yield return new Opportunity(
                $"duplicate_subscription:{group.Key}", OpportunityType.DuplicateSubscription,
                string.Join(", ", members.Select(s => s.Merchant)), total - MonthlyCost(members[0]), total, members.Count,
                Difficulty.Easy, GroupTransactionIds(snapshot, members));
        }
    }

    public static long MonthlyCost(RecurringGroup group) => group.PeriodDays == RecurringDetector.WeeklyPeriodDays
        ? Money.Round(-group.AvgAmountGr * 52m / 12m)
        : -group.AvgAmountGr;

    /// <summary>Positive spending of <paramref name="transactions"/> scaled to a 30-day month.</summary>
    public static long Monthly(IEnumerable<Transaction> transactions, DateRangeDays window) =>
        Money.Round(-transactions.Sum(t => t.AmountGr) * 30m / window.Days);

    private static int MonthlyCount(int count, DateRangeDays window) =>
        (int)Math.Round(count * 30m / window.Days, MidpointRounding.AwayFromZero);

    private static string TopMerchants(IEnumerable<Transaction> transactions) => string.Join(", ", transactions
        .GroupBy(t => t.Merchant)
        .OrderByDescending(g => g.Count())
        .ThenBy(g => g.Key, StringComparer.Ordinal)
        .Take(3)
        .Select(g => g.Key));

    private static List<Guid> Ids(IEnumerable<Transaction> transactions) =>
        transactions.OrderByDescending(t => t.Date).Select(t => t.Id).ToList();

    private static List<Guid> GroupTransactionIds(FinancialSnapshot snapshot, IReadOnlyCollection<RecurringGroup> groups)
    {
        var ids = groups.Select(g => g.Id).ToHashSet();
        return Ids(snapshot.Transactions.Where(t => t.RecurringGroupId is { } id && ids.Contains(id)));
    }
}

using CashCoach.Core.Domain;

namespace CashCoach.Core.Services;

/// <param name="AmountGr">Positive amount of one charge in grosze.</param>
/// <param name="Group"><c>music</c>, <c>video</c> or <c>null</c> for services that have no overlapping alternatives.</param>
/// <param name="Duplicate">True when another active subscription is in the same group.</param>
public sealed record SubscriptionItem(RecurringGroup Subscription, long AmountGr, string? Group, bool Duplicate);

/// <param name="MonthlyTotalGr">Positive monthly cost of all items in grosze; weekly charges are scaled to a month.</param>
public sealed record SubscriptionOverview(long MonthlyTotalGr, IReadOnlyList<SubscriptionItem> Items);

public static class SubscriptionOverviewCalculator
{
    public const string MusicGroup = "music";
    public const string VideoGroup = "video";

    private static readonly Dictionary<string, string> Groups = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spotify"] = MusicGroup,
        ["Tidal"] = MusicGroup,
        ["YouTube Music"] = MusicGroup,
        ["Apple Music"] = MusicGroup,
        ["Netflix"] = VideoGroup,
        ["HBO Max"] = VideoGroup,
        ["Disney+"] = VideoGroup,
        ["Prime"] = VideoGroup,
        ["Prime Video"] = VideoGroup,
    };

    public static string? GroupOf(string merchant) => Groups.GetValueOrDefault(merchant);

    public static SubscriptionOverview Build(IEnumerable<RecurringGroup> subscriptions)
    {
        var list = subscriptions.ToList();
        var groupSizes = list
            .Select(subscription => GroupOf(subscription.Merchant))
            .OfType<string>()
            .GroupBy(group => group)
            .ToDictionary(group => group.Key, group => group.Count());

        var items = list
            .Select(subscription =>
            {
                var group = GroupOf(subscription.Merchant);
                return new SubscriptionItem(
                    subscription,
                    Math.Abs(subscription.AvgAmountGr),
                    group,
                    group is not null && groupSizes[group] > 1);
            })
            .OrderByDescending(item => item.Duplicate)
            .ThenByDescending(item => item.AmountGr)
            .ThenBy(item => item.Subscription.Merchant, StringComparer.Ordinal)
            .ToList();

        var monthlyTotalGr = items.Sum(item => item.Subscription.PeriodDays == RecurringDetector.WeeklyPeriodDays
            ? Money.Round(item.AmountGr * 52m / 12m)
            : item.AmountGr);

        return new SubscriptionOverview(monthlyTotalGr, items);
    }
}

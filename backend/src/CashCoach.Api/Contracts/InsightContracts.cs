using CashCoach.Core.Domain;
using CashCoach.Core.Services;

namespace CashCoach.Api.Contracts;

public sealed record MerchantSummaryDto(string Merchant, decimal Amount, int Count);

/// <param name="Share">Fraction of <c>total_spent</c>, 0 to 1.</param>
/// <param name="VsPrevPct">Percent change against the previous period of the same length; <c>null</c> when nothing was spent then.</param>
public sealed record CategorySummaryDto(
    Category Category,
    decimal Amount,
    int Count,
    decimal Share,
    decimal? VsPrevPct,
    IReadOnlyList<MerchantSummaryDto> TopMerchants);

/// <param name="TotalSpent">Positive złoty total of all expenses in the period.</param>
public sealed record SummaryResponse(PeriodDto Period, decimal TotalSpent, IReadOnlyList<CategorySummaryDto> Categories)
{
    public static SummaryResponse From(SpendingSummary summary) => new(
        PeriodDto.Of(summary.Period),
        Money.ToZloty(summary.TotalSpentGr),
        summary.Categories.Select(category => new CategorySummaryDto(
            category.Category,
            Money.ToZloty(category.AmountGr),
            category.Count,
            category.Share,
            category.VsPrevPct,
            category.TopMerchants.Select(merchant => new MerchantSummaryDto(merchant.Merchant, Money.ToZloty(merchant.AmountGr), merchant.Count)).ToList()))
            .ToList());
}

/// <param name="Amount">Positive złoty amount of one charge.</param>
/// <param name="Group"><c>music</c>, <c>video</c> or <c>null</c>.</param>
/// <param name="UserConfirmed">The user's answer to "still using it?"; <c>null</c> until answered.</param>
public sealed record SubscriptionDto(Guid Id, string Merchant, decimal Amount, DateOnly? NextDate, string? Group, bool Duplicate, bool? UserConfirmed);

public sealed record SubscriptionsResponse(decimal MonthlyTotal, IReadOnlyList<SubscriptionDto> Items)
{
    public static SubscriptionsResponse From(SubscriptionOverview overview) => new(
        Money.ToZloty(overview.MonthlyTotalGr),
        overview.Items.Select(item => new SubscriptionDto(
            item.Subscription.Id,
            item.Subscription.Merchant,
            Money.ToZloty(item.AmountGr),
            item.Subscription.NextDate,
            item.Group,
            item.Duplicate,
            item.Subscription.UserConfirmed)).ToList());
}

public sealed record UpdateSubscriptionRequest(bool? StillUsing);

/// <param name="NewOpportunityId">When the user no longer uses it, the id of the new <c>unused_subscription</c> opportunity in <c>GET /opportunities</c>; otherwise <c>null</c>.</param>
public sealed record UpdateSubscriptionResponse(Guid Id, bool? UserConfirmed, string? NewOpportunityId);

/// <param name="Instalment">Positive złoty amount of one instalment.</param>
/// <param name="Remaining">Positive złoty amount still to pay.</param>
public sealed record BnplPlanDto(string Provider, string Merchant, decimal Instalment, int Paid, int Total, decimal Remaining, DateOnly? NextDate);

public sealed record BnplResponse(int ActivePlans, decimal TotalRemaining, IReadOnlyList<BnplPlanDto> Items, string Explainer)
{
    public static BnplResponse From(BnplSummary summary, string language) => new(
        summary.ActivePlans,
        Money.ToZloty(summary.TotalRemainingGr),
        summary.Plans.Select(plan => new BnplPlanDto(
            plan.Provider,
            plan.Merchant,
            Money.ToZloty(plan.InstalmentGr),
            plan.Paid,
            plan.Total,
            Money.ToZloty(plan.RemainingGr),
            plan.NextDate)).ToList(),
        BnplExplainer.Build(summary, language));
}

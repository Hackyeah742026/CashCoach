using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Ai;
using CashCoach.Infrastructure.Analytics;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class HomeEndpoints
{
    public static RouteGroupBuilder MapHomeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/home", async (
                string? month, CurrentUser user, SnapshotLoader loader, AnalyticsService analytics, WrappedCaptionService copy, CancellationToken ct) =>
            {
                var language = user.User.Language;
                var snapshot = await loader.LoadAsync(user.Id, ct);
                var current = new DateOnly(snapshot.AsOf.Year, snapshot.AsOf.Month, 1);
                var selected = month is null ? current : AnalyticsEndpoints.ParseMonth(month);
                if (!WrappedBuilder.AvailableMonths(snapshot.Transactions).Contains(selected))
                {
                    throw NotFound("month_not_found", "No transactions in this month.");
                }

                var forecast = ForecastCalculator.Compute(snapshot);
                var opportunities = await analytics.OpportunitiesAsync(snapshot, user.Id, ct);
                var stats = WrappedBuilder.Build(snapshot.Transactions, selected, opportunities.Sum(o => o.MonthlySavingGr));
                var narrative = NarrativeBuilder.Build(snapshot.Transactions, stats, selected == current ? forecast : null, opportunities, language);
                var ai = await copy.NarrativeAsync(user.Id, selected, narrative, language, ct);

                return TypedResults.Ok(new HomeResponse(
                    Zl.Month(selected),
                    snapshot.AsOf,
                    Zl.Of(stats.TotalIncomeGr),
                    -Zl.Of(stats.TotalSpentGr),
                    Zl.Of(stats.TotalIncomeGr - stats.TotalSpentGr),
                    stats.ChangePct,
                    Zl.Of(forecast.SafeToSpendGr),
                    new EvidenceDto(
                        forecast.UpcomingTransactionIds,
                        ForecastResponse.Figures(forecast, language).Take(3).ToList(),
                        $"{Plain(forecast.BalanceGr)} − {Plain(forecast.FixedUpcomingGr)} − {Plain(forecast.SafetyBufferGr)} = {Plain(forecast.SafeToSpendGr)}"),
                    new PayPeriodDto(ForecastCalculator.PreviousPayday(snapshot.AsOf, snapshot.Payday), forecast.NextPayday, forecast.DaysLeft),
                    forecast.Status,
                    WrappedBuilder.CategoryChanges(snapshot.Transactions, selected)
                        .Select(c => new HomeCategoryDto(c.Category, -Zl.Of(c.AmountGr), c.ChangePct))
                        .ToList(),
                    Recurring(snapshot),
                    new NarrativeDto(
                        ai.GetValueOrDefault("headline") ?? narrative.Headline,
                        narrative.Bullets.Select(b => new NarrativeBulletDto(ai.GetValueOrDefault(b.Id) ?? b.Text, b.FactKeys, b.TransactionIds)).ToList(),
                        ai.Count > 0,
                        ai.Count > 0 ? FactCheck.Passed : FactCheck.Fallback)));
            })
            .WithName("GetHome")
            .WithTags("Analytics");

        return api;
    }

    /// <summary>Active subscriptions, rent and BNPL plans, soonest first.</summary>
    private static List<HomeRecurringDto> Recurring(FinancialSnapshot snapshot) => snapshot.Recurring
        .Where(g => g.Active && g.AvgAmountGr < 0 && g.Type is RecurringType.Subscription or RecurringType.Rent or RecurringType.Bnpl)
        .OrderBy(g => g.NextDate ?? DateOnly.MaxValue)
        .Select(g => new HomeRecurringDto(
            g.Merchant,
            g.Type,
            Zl.Of(g.AvgAmountGr),
            g.PeriodDays == 7 ? "weekly" : "monthly",
            g.NextDate,
            snapshot.Transactions.Where(t => t.RecurringGroupId == g.Id).OrderByDescending(t => t.Date).Select(t => t.Id).ToList()))
        .ToList();

    private static string Plain(long grosze) => Money.ToZloty(grosze).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
}

using System.Globalization;
using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Ai;
using CashCoach.Infrastructure.Analytics;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static RouteGroupBuilder MapAnalyticsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/forecast", async (CurrentUser user, SnapshotLoader loader, CancellationToken ct) =>
            {
                var snapshot = await loader.LoadAsync(user.Id, ct);
                return TypedResults.Ok(ForecastResponse.From(ForecastCalculator.Compute(snapshot), user.User.Language));
            })
            .WithName("GetForecast")
            .WithTags("Analytics");

        api.MapGet("/opportunities", async (
                CurrentUser user, SnapshotLoader loader, AnalyticsService analytics, WrappedCaptionService copy, CancellationToken ct) =>
            {
                var snapshot = await loader.LoadAsync(user.Id, ct);
                var opportunities = await analytics.OpportunitiesAsync(snapshot, user.Id, ct);
                var rationales = await copy.OpportunityRationalesAsync(user.Id, opportunities, user.User.Language, ct);
                return TypedResults.Ok(ToResponse(opportunities, user.User.Language, rationales));
            })
            .WithName("ListOpportunities")
            .WithTags("Analytics");

        api.MapPost("/opportunities/{id}/dismiss", async (string id, CurrentUser user, DismissalService dismissals, CancellationToken ct) =>
            {
                await dismissals.DismissAsync(user.Id, DismissalService.OpportunityPrefix + id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DismissOpportunity")
            .WithTags("Analytics");

        api.MapPost("/simulate/purchase", async (SimulatePurchaseRequest request, CurrentUser user, SnapshotLoader loader, CancellationToken ct) =>
            {
                var amount = ToGrosze(request.Amount, "amount", 0.01m);
                var snapshot = await loader.LoadAsync(user.Id, ct);
                if (request.Assumptions is { } what)
                {
                    if (what.Payday is < 1 or > 31)
                    {
                        throw BadRequest("invalid_payday", "'payday' must be a day of month from 1 to 31.");
                    }

                    snapshot = snapshot with
                    {
                        Payday = what.Payday ?? snapshot.Payday,
                        SafetyBufferGr = what.SafetyBuffer is null ? snapshot.SafetyBufferGr : ToGrosze(what.SafetyBuffer, "safety_buffer", 0m, 100_000m),
                        BalanceGr = what.Balance is null ? snapshot.BalanceGr : ToGrosze(what.Balance, "balance", -10_000_000m),
                    };
                }

                var simulation = Simulator.SimulatePurchase(snapshot, amount, request.Date);
                return TypedResults.Ok(ToResponse(simulation, snapshot, request.Item, user.User.Language));
            })
            .WithName("SimulatePurchase")
            .WithTags("Simulations");

        api.MapPost("/simulate/change", async (SimulateChangeRequest request, CurrentUser user, SnapshotLoader loader, CancellationToken ct) =>
            {
                var category = ParseEnum<Category>(request.Category, "category");
                var perWeek = ToGrosze(request.NewPerWeek, "new_per_week", 0m);
                var snapshot = await loader.LoadAsync(user.Id, ct);
                var change = Simulator.SimulateChange(snapshot, category, perWeek);
                var en = CoachTexts.IsEnglish(user.User.Language);
                return TypedResults.Ok(new SimulateChangeResponse(
                    change.Category,
                    Zl.Of(change.CurrentPerWeekGr),
                    Zl.Of(change.CurrentPerMonthGr),
                    Zl.Of(change.NewPerWeekGr),
                    Zl.Of(change.MonthlySavingGr),
                    Zl.Of(change.YearlySavingGr),
                    change.Goals.Select(GoalImpactDto.From).ToList(),
                    new EvidenceDto(change.TransactionIds,
                    [
                        new("change.current_per_week", en ? "Spent per week now" : "Obecnie tygodniowo", Zl.Of(change.CurrentPerWeekGr)),
                        new("change.monthly_saving", en ? "Saving per month" : "Oszczędność miesięcznie", Zl.Of(change.MonthlySavingGr)),
                    ])));
            })
            .WithName("SimulateChange")
            .WithTags("Simulations");

        api.MapGet("/alerts", async (CurrentUser user, SnapshotLoader loader, AnalyticsService analytics, CancellationToken ct) =>
            {
                var snapshot = await loader.LoadAsync(user.Id, ct);
                var alerts = await analytics.AlertsAsync(snapshot, ForecastCalculator.Compute(snapshot), user.Id, ct);
                return TypedResults.Ok(alerts.Select(a => AlertDto.From(a, user.User.Language)).ToList());
            })
            .WithName("ListAlerts")
            .WithTags("Analytics");

        api.MapPost("/alerts/{id}/dismiss", async (string id, CurrentUser user, DismissalService dismissals, CancellationToken ct) =>
            {
                await dismissals.DismissAsync(user.Id, DismissalService.AlertPrefix + id, ct);
                return TypedResults.NoContent();
            })
            .WithName("DismissAlert")
            .WithTags("Analytics");

        api.MapGet("/dashboard", async (
                CurrentUser user, SnapshotLoader loader, AnalyticsService analytics, CancellationToken ct) =>
            {
                var language = user.User.Language;
                var snapshot = await loader.LoadAsync(user.Id, ct);
                var forecast = ForecastCalculator.Compute(snapshot);
                var opportunities = await analytics.OpportunitiesAsync(snapshot, user.Id, ct);
                var alerts = await analytics.AlertsAsync(snapshot, forecast, user.Id, ct);
                var month = new DateOnly(snapshot.AsOf.Year, snapshot.AsOf.Month, 1);
                var stats = WrappedBuilder.Build(snapshot.Transactions, month, opportunities.Sum(o => o.MonthlySavingGr));
                var subscriptions = AnalyticsService.Subscriptions(snapshot);
                var bnpl = AnalyticsService.Bnpl(snapshot);
                var nextBnpl = bnpl.Plans.Where(p => p.Active && p.NextDate is not null).MinBy(p => p.NextDate);

                return TypedResults.Ok(new DashboardResponse(
                    new DashboardUserDto(user.User.Name, user.User.Persona, language),
                    snapshot.AsOf,
                    ForecastResponse.From(forecast, language),
                    new MonthTotalsDto(
                        Zl.Month(month),
                        Zl.Of(stats.TotalIncomeGr),
                        -Zl.Of(stats.TotalSpentGr),
                        Zl.Of(stats.TotalIncomeGr - stats.TotalSpentGr),
                        stats.TopCategories.Select(c => new WrappedCategoryDto(c.Category, Zl.Of(c.AmountGr), c.SharePct)).ToList()),
                    opportunities.Take(3).Select(o => OpportunityDto.From(o, language)).ToList(),
                    Zl.Of(opportunities.Sum(o => o.MonthlySavingGr)),
                    snapshot.Goals.Select(g => GoalDto.From(GoalCalculator.Progress(g, snapshot.AsOf))).ToList(),
                    alerts.Select(a => AlertDto.From(a, language)).ToList(),
                    new SubscriptionsBriefDto(Zl.Of(subscriptions.MonthlyTotalGr), subscriptions.Items.Count, subscriptions.Items.Count(i => i.Duplicate)),
                    new BnplBriefDto(bnpl.ActivePlans, Zl.Of(bnpl.TotalRemainingGr), nextBnpl?.NextDate, Zl.Of(nextBnpl?.InstalmentGr))));
            })
            .WithName("GetDashboard")
            .WithTags("Analytics");

        return api;
    }

    public static OpportunitiesResponse ToResponse(IReadOnlyList<Opportunity> opportunities, string language, IReadOnlyDictionary<string, string>? aiRationales = null)
    {
        var monthly = opportunities.Sum(o => o.MonthlySavingGr);
        return new OpportunitiesResponse(
            Zl.Of(monthly),
            Zl.Of(monthly * 12),
            opportunities.Select(o => OpportunityDto.From(o, language, aiRationales?.GetValueOrDefault(o.Id))).ToList());
    }

    private static SimulatePurchaseResponse ToResponse(PurchaseSimulation s, FinancialSnapshot snapshot, string? item, string language)
    {
        var en = CoachTexts.IsEnglish(language);
        var price = CoachTexts.Zl(s.AmountGr, language);
        var safe = CoachTexts.Zl(s.Before.SafeToSpendGr, language);
        var explanation = s.Verdict switch
        {
            Verdict.Green => en
                ? $"It fits: you can safely spend {safe} before payday, and {price} leaves {CoachTexts.Zl(s.LeftAfterGr ?? 0, language)}."
                : $"Mieści się: do wypłaty możesz bezpiecznie wydać {safe}, a po zakupie za {price} zostanie {CoachTexts.Zl(s.LeftAfterGr ?? 0, language)}.",
            Verdict.Yellow => en
                ? $"Only with your safety buffer: safe to spend is {safe}, so you would be short {CoachTexts.Zl(s.ShortfallGr ?? 0, language)}. Consider waiting for payday on {CoachTexts.Date(s.Before.NextPayday, language)}."
                : $"Tylko kosztem poduszki bezpieczeństwa: bezpiecznie możesz wydać {safe}, więc zabraknie {CoachTexts.Zl(s.ShortfallGr ?? 0, language)}. Rozważ zakup po wypłacie ({CoachTexts.Date(s.Before.NextPayday, language)}).",
            _ => en
                ? $"Not now: safe to spend is {safe} and the purchase is {price}. You would be short {CoachTexts.Zl(s.ShortfallGr ?? 0, language)}."
                : $"Nie teraz: bezpiecznie możesz wydać {safe}, a zakup to {price}. Zabraknie {CoachTexts.Zl(s.ShortfallGr ?? 0, language)}.",
        };

        return new SimulatePurchaseResponse(
            item,
            Zl.Of(s.AmountGr),
            s.Date,
            s.BeforePayday,
            s.Verdict,
            Zl.Of(s.Before.SafeToSpendGr),
            Zl.Of(s.LeftAfterGr),
            Zl.Of(s.ShortfallGr),
            ForecastBriefDto.From(s.Before),
            ForecastBriefDto.From(s.After),
            ForecastResponse.Figures(s.Before, language).Take(4).ToList(),
            s.GoalDelays.Select(GoalImpactDto.From).ToList(),
            new AssumptionsDto(snapshot.Payday, Zl.Of(snapshot.SafetyBufferGr), Zl.Of(snapshot.BalanceGr)),
            explanation,
            new EvidenceDto(s.Before.UpcomingTransactionIds, ForecastResponse.Figures(s.Before, language)));
    }

    /// <summary>Parses <c>YYYY-MM</c>.</summary>
    public static DateOnly ParseMonth(string? month) =>
        DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw BadRequest("invalid_month", "'month' must be YYYY-MM.");
}

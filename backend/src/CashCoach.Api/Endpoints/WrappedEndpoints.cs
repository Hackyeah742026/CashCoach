using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Analytics;
using CashCoach.Infrastructure.Ai;
using CashCoach.Infrastructure.Analytics;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class WrappedEndpoints
{
    public static RouteGroupBuilder MapWrappedEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/wrapped/months", async (CurrentUser user, SnapshotLoader loader, CancellationToken ct) =>
            {
                var months = WrappedBuilder.AvailableMonths((await loader.LoadAsync(user.Id, ct)).Transactions);
                return TypedResults.Ok(months.Select((month, index) => new WrappedMonthDto(Zl.Month(month), index == 0)).ToList());
            })
            .WithName("ListWrappedMonths")
            .WithTags("Wrapped");

        api.MapGet("/wrapped", async (string? month, CurrentUser user, SnapshotLoader loader, AnalyticsService analytics, WrappedCaptionService captions, CancellationToken ct) =>
            {
                var snapshot = await loader.LoadAsync(user.Id, ct);
                var selected = month is null
                    ? WrappedBuilder.AvailableMonths(snapshot.Transactions).FirstOrDefault()
                    : AnalyticsEndpoints.ParseMonth(month);
                var stats = await analytics.WrappedAsync(snapshot, user.Id, selected, ct)
                    ?? throw NotFound("month_not_found", "No transactions in this month.");
                var copy = await captions.GetAsync(user.Id, stats, user.User.Language, ct);
                return TypedResults.Ok(WrappedMapper.ToResponse(stats, copy, user.User.Language));
            })
            .WithName("GetWrapped")
            .WithTags("Wrapped");

        return api;
    }
}

public static class WrappedMapper
{
    public static WrappedResponse ToResponse(WrappedStats s, WrappedCopy copy, string language)
    {
        var en = CoachTexts.IsEnglish(language);
        var top = s.TopCategories.FirstOrDefault();
        var (personalityTitle, personalityDescription) = CoachTexts.PersonalityText(s.Personality, language);

        WrappedCardDto Card(string type, IReadOnlyList<FigureDto> figures, IReadOnlyList<Guid>? ids = null)
        {
            var (title, template) = CoachTexts.WrappedCardText(type, s, language);
            var ai = type == WrappedCardTypes.Personality ? copy.PersonalityDescription : copy.Captions.GetValueOrDefault(type);
            return new WrappedCardDto(type, title, ai ?? template, figures, new EvidenceDto(ids ?? [], figures), ai is not null);
        }

        FigureDto F(string key, string pl, string english, long grosze) => new($"wrapped.{key}", en ? english : pl, Zl.Of(grosze));

        var cards = new List<WrappedCardDto>
        {
            Card(WrappedCardTypes.TotalSpent, [F("total_spent", "Wydatki", "Spent", s.TotalSpentGr), F("total_income", "Wpływy", "Income", s.TotalIncomeGr)]),
            Card(WrappedCardTypes.TopCategories,
                s.TopCategories.Select(c => F($"category.{c.Category.ToString().ToLowerInvariant()}", CoachTexts.CategoryName(c.Category, "pl"), CoachTexts.CategoryName(c.Category, "en"), c.AmountGr)).ToList()),
            Card(WrappedCardTypes.TopMerchant,
                s.TopMerchant is { } m ? [F("top_merchant", m.Name, m.Name, m.AmountGr)] : [], s.TopMerchant?.TransactionIds),
            Card(WrappedCardTypes.Delivery, [F("delivery", "Dostawy", "Delivery", s.Delivery.AmountGr)], s.Delivery.TransactionIds),
            Card(WrappedCardTypes.BiggestDay,
                s.BiggestDay is { } d ? [F("biggest_day", CoachTexts.Date(d.Date, "pl"), CoachTexts.Date(d.Date, "en"), d.AmountGr)] : [], s.BiggestDay?.TransactionIds),
            Card(WrappedCardTypes.Subscriptions, [F("subscriptions", "Subskrypcje", "Subscriptions", s.Subscriptions.AmountGr)], s.Subscriptions.TransactionIds),
            Card(WrappedCardTypes.MonthOverMonth,
                [F("previous_spent", "Poprzedni miesiąc", "Previous month", s.PreviousSpentGr), F("total_spent", "Ten miesiąc", "This month", s.TotalSpentGr)]),
            Card(WrappedCardTypes.Personality, [F("potential_savings", "Możliwe oszczędności", "Potential savings", s.PotentialSavingsGr)]),
        };

        return new WrappedResponse(
            Zl.Month(s.Month),
            Zl.Of(s.TotalSpentGr),
            Zl.Of(s.TotalIncomeGr),
            s.ChangePct,
            s.TransactionCount,
            s.TopMerchant is { } merchant
                ? new WrappedMerchantDto(merchant.Name, merchant.Count, Zl.Of(merchant.AmountGr), new EvidenceDto(merchant.TransactionIds, []))
                : null,
            top is null ? null : new WrappedCategoryDto(top.Category, Zl.Of(top.AmountGr), top.SharePct),
            s.TopCategories.Select(c => new WrappedCategoryDto(c.Category, Zl.Of(c.AmountGr), c.SharePct)).ToList(),
            s.BiggestChange is { } change ? new WrappedChangeDto(change.Category, Zl.Of(change.FromGr), Zl.Of(change.ToGr), change.ChangePct) : null,
            s.CheapestWeekday.ToString().ToLowerInvariant(),
            Zl.Of(s.PotentialSavingsGr),
            new PersonalityDto(
                s.Personality.Key,
                s.Personality.Emoji,
                personalityTitle,
                copy.PersonalityDescription ?? personalityDescription,
                copy.PersonalityDescription is not null),
            new WrappedCaptionsDto(
                cards.Single(c => c.Type == WrappedCardTypes.TotalSpent).Caption,
                cards.Single(c => c.Type == WrappedCardTypes.TopMerchant).Caption,
                copy.Captions.GetValueOrDefault(WrappedCaptionService.BiggestChangeKey) ?? CoachTexts.BiggestChangeCaption(s, language)),
            cards);
    }
}

using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;

namespace CashCoach.Infrastructure.Ai;

/// <param name="Captions">Verified AI captions by card type (plus <c>biggest_change</c>); missing ones use the template.</param>
/// <param name="PersonalityDescription">Verified AI description, or <c>null</c> for the template.</param>
public sealed record WrappedCopy(IReadOnlyDictionary<string, string> Captions, string? PersonalityDescription)
{
    public static readonly WrappedCopy Templates = new(new Dictionary<string, string>(), null);
}

/// <summary>AI captions for Wrapped cards and opportunity explanations, fact-checked and cached per user, month and language.</summary>
public sealed class WrappedCaptionService(AiCopywriter copywriter)
{
    private const string PersonalityKey = "personality_description";
    public const string BiggestChangeKey = "biggest_change";

    public async Task<WrappedCopy> GetAsync(Guid userId, WrappedStats stats, string language, CancellationToken cancellationToken)
    {
        var templates = WrappedCardTypes.All
            .Where(type => type != WrappedCardTypes.Personality)
            .ToDictionary(type => type, type => CoachTexts.WrappedCardText(type, stats, language).Caption);
        templates[PersonalityKey] = CoachTexts.PersonalityText(stats.Personality, language).Description;
        templates[BiggestChangeKey] = CoachTexts.BiggestChangeCaption(stats, language);

        var facts = new
        {
            total_spent = Money.ToZloty(stats.TotalSpentGr),
            total_income = Money.ToZloty(stats.TotalIncomeGr),
            previous_month_spent = Money.ToZloty(stats.PreviousSpentGr),
            change_pct = stats.ChangePct,
            transaction_count = stats.TransactionCount,
            top_categories = stats.TopCategories.Select(c => new { category = CoachTexts.CategoryName(c.Category, language), amount = Money.ToZloty(c.AmountGr), share_pct = c.SharePct }),
            top_merchant = stats.TopMerchant is { } m ? new { name = m.Name, visits = m.Count, amount = Money.ToZloty(m.AmountGr) } : null,
            delivery = new { orders = stats.Delivery.Count, amount = Money.ToZloty(stats.Delivery.AmountGr), pizzas_equivalent = stats.Fun.Count },
            biggest_day = stats.BiggestDay is { } d ? new { date = d.Date, amount = Money.ToZloty(d.AmountGr), payments = d.Count } : null,
            subscriptions = new { count = stats.Subscriptions.Count, amount = Money.ToZloty(stats.Subscriptions.AmountGr) },
            biggest_change = stats.BiggestChange is { } c
                ? new { category = CoachTexts.CategoryName(c.Category, language), from = Money.ToZloty(c.FromGr), to = Money.ToZloty(c.ToGr), change_pct = c.ChangePct }
                : null,
            personality = CoachTexts.PersonalityText(stats.Personality, language).Title,
            potential_savings_per_month = Money.ToZloty(stats.PotentialSavingsGr),
        };

        var rewritten = await copywriter.RewriteAsync(
            $"wrapped:{userId}:{stats.Month:yyyy-MM}:{language}:{stats.TotalSpentGr}:{stats.PotentialSavingsGr}",
            "Captions for a Spotify-Wrapped-style monthly spending story. Playful, short, a bit of humour.",
            templates, facts, language, cancellationToken);

        return new WrappedCopy(
            rewritten.Where(pair => pair.Key != PersonalityKey).ToDictionary(),
            rewritten.GetValueOrDefault(PersonalityKey));
    }

    /// <summary>AI rephrasing of the Home headline and bullets; ids are <c>headline</c> and the bullet ids.</summary>
    public Task<IReadOnlyDictionary<string, string>> NarrativeAsync(Guid userId, DateOnly month, Narrative narrative, string language, CancellationToken cancellationToken)
    {
        var templates = narrative.Bullets.ToDictionary(b => b.Id, b => b.Text);
        templates["headline"] = narrative.Headline;
        var version = string.Join(",", narrative.Facts.OrderBy(f => f.Key).Select(f => $"{f.Key}={f.Value}"));

        return copywriter.RewriteAsync(
            $"narrative:{userId}:{month:yyyy-MM}:{language}:{version.GetHashCode()}",
            "A short monthly spending summary for the home screen: one headline and a few bullet points. Friendly, concrete, no judgement.",
            templates, narrative.Facts, language, cancellationToken);
    }

    /// <returns>Verified AI rationales by opportunity id.</returns>
    public Task<IReadOnlyDictionary<string, string>> OpportunityRationalesAsync(
        Guid userId, IReadOnlyList<Opportunity> opportunities, string language, CancellationToken cancellationToken)
    {
        if (opportunities.Count == 0)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        }

        var templates = opportunities.ToDictionary(o => o.Id, o => CoachTexts.OpportunityRationale(o, language));
        var facts = opportunities.Select(o => new
        {
            id = o.Id,
            about = o.Subject,
            monthly_spend = Money.ToZloty(o.MonthlySpendGr),
            monthly_saving = Money.ToZloty(o.MonthlySavingGr),
            yearly_saving = Money.ToZloty(o.YearlySavingGr),
            payments_per_month = o.Count,
        }).ToList();
        var version = string.Join(",", opportunities.Select(o => $"{o.Id}={o.MonthlySavingGr}"));

        return copywriter.RewriteAsync(
            $"opportunities:{userId}:{language}:{version}",
            "Explanations of savings ideas: concrete and kind, mention the yearly saving when it motivates.",
            templates, facts, language, cancellationToken);
    }
}

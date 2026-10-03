using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

/// <param name="FactKeys">Keys of the computed figures the sentence uses, e.g. <c>cat.food_delivery.month</c>.</param>
public sealed record NarrativeBullet(string Id, string Text, IReadOnlyList<string> FactKeys, IReadOnlyList<Guid> TransactionIds);

/// <param name="Facts">Every figure the texts may mention, by fact key (złoty or plain numbers).</param>
public sealed record Narrative(string Headline, IReadOnlyList<NarrativeBullet> Bullets, IReadOnlyDictionary<string, decimal> Facts);

/// <summary>The "where did my money go" headline and bullets for a month, as deterministic templates (AI may rephrase them).</summary>
public static class NarrativeBuilder
{
    /// <param name="forecast">Included only for the current month (the one containing <see cref="FinancialSnapshot.AsOf"/>).</param>
    public static Narrative Build(
        IReadOnlyList<Transaction> transactions, WrappedStats stats, Forecast? forecast, IReadOnlyList<Opportunity> opportunities, string language)
    {
        var en = CoachTexts.IsEnglish(language);
        string Z(long gr) => CoachTexts.Zl(gr, language);
        var month = CoachTexts.MonthName(stats.Month, language);
        var facts = new Dictionary<string, decimal>
        {
            ["month.total_spent"] = Money.ToZloty(stats.TotalSpentGr),
            ["month.income"] = Money.ToZloty(stats.TotalIncomeGr),
        };

        string headline;
        if (stats.ChangePct is { } pct)
        {
            facts["month.change_pct"] = Math.Abs(pct);
            var direction = pct >= 0 ? (en ? "more" : "więcej") : (en ? "less" : "mniej");
            headline = en
                ? $"{month}: you spent {Z(stats.TotalSpentGr)}, {Math.Abs(pct)}% {direction} than the month before."
                : $"{Capitalize(month)}: wydatki {Z(stats.TotalSpentGr)} — o {Math.Abs(pct)}% {direction} niż miesiąc wcześniej.";
        }
        else
        {
            headline = en ? $"{month}: you spent {Z(stats.TotalSpentGr)}." : $"{Capitalize(month)}: wydatki {Z(stats.TotalSpentGr)}.";
        }

        var bullets = new List<NarrativeBullet>();
        var categories = WrappedBuilder.CategoryChanges(transactions, stats.Month);
        var discretionary = categories.FirstOrDefault(c => c.Category is not (Category.Rent or Category.Utilities or Category.Transfers or Category.Bnpl));
        if (discretionary.TransactionIds is not null)
        {
            var key = $"cat.{SnakeCaseEnum<Category>.ToName(discretionary.Category)}.month";
            facts[key] = Money.ToZloty(discretionary.AmountGr);
            var name = CoachTexts.CategoryName(discretionary.Category, language);
            var change = discretionary.ChangePct is { } c && Math.Abs(c) >= 10
                ? (en ? $", {Math.Abs(decimal.Round(c))}% {(c > 0 ? "more" : "less")} than before" : $", o {Math.Abs(decimal.Round(c))}% {(c > 0 ? "więcej" : "mniej")} niż wcześniej")
                : "";
            if (change.Length > 0)
            {
                facts[key + ".change_pct"] = Math.Abs(decimal.Round(discretionary.ChangePct!.Value));
            }

            bullets.Add(new NarrativeBullet("b_category",
                en ? $"Biggest everyday category: {name}, {Z(discretionary.AmountGr)}{change}." : $"Największa codzienna kategoria: {name}, {Z(discretionary.AmountGr)}{change}.",
                [key], discretionary.TransactionIds));
        }

        if (stats.Delivery.Count > 0)
        {
            facts["cat.food_delivery.count"] = stats.Delivery.Count;
            facts["cat.food_delivery.total"] = Money.ToZloty(stats.Delivery.AmountGr);
            bullets.Add(new NarrativeBullet("b_delivery",
                en ? $"Food delivery: {Z(stats.Delivery.AmountGr)} for {stats.Delivery.Count} orders."
                   : $"Dostawy jedzenia: {Z(stats.Delivery.AmountGr)} za {stats.Delivery.Count} {CoachTexts.PolishPlural(stats.Delivery.Count, "zamówienie", "zamówienia", "zamówień")}.",
                ["cat.food_delivery.total", "cat.food_delivery.count"], stats.Delivery.TransactionIds));
        }

        if (forecast is not null)
        {
            facts["forecast.safe_to_spend"] = Money.ToZloty(forecast.SafeToSpendGr);
            facts["forecast.days_left"] = forecast.DaysLeft;
            var text = forecast.Status == ForecastStatus.Danger && forecast.RunOutDate is { } runOut
                ? en ? $"At this pace the money runs out on {CoachTexts.Date(runOut, language)}, before payday." : $"W tym tempie pieniądze skończą się {CoachTexts.Date(runOut, language)}, jeszcze przed wypłatą."
                : en ? $"You can safely spend {Z(forecast.SafeToSpendGr)} in the {forecast.DaysLeft} days until payday." : $"Do wypłaty ({forecast.DaysLeft} dni) możesz bezpiecznie wydać {Z(forecast.SafeToSpendGr)}.";
            bullets.Add(new NarrativeBullet("b_forecast", text, ["forecast.safe_to_spend", "forecast.days_left"], forecast.UpcomingTransactionIds));
        }

        if (opportunities.FirstOrDefault() is { } top)
        {
            var key = $"opportunity.{top.Id}.monthly_saving";
            facts[key] = Money.ToZloty(top.MonthlySavingGr);
            bullets.Add(new NarrativeBullet("b_saving",
                en ? $"Idea: {CoachTexts.OpportunityTitle(top, language).ToLowerInvariant()} to save {Z(top.MonthlySavingGr)} a month."
                   : $"Pomysł: {CoachTexts.OpportunityTitle(top, language).ToLowerInvariant()}, czyli {Z(top.MonthlySavingGr)} oszczędności miesięcznie.",
                [key], top.TransactionIds));
        }

        return new Narrative(headline, bullets, facts);
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}

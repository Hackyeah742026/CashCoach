using System.Globalization;
using CashCoach.Core.Domain;

namespace CashCoach.Core.Services;

/// <summary>A deterministic, template-based explanation of the user's BNPL plans (no LLM).</summary>
public static class BnplExplainer
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static string Build(BnplSummary summary, string language)
    {
        var next = summary.Plans.Where(plan => plan.Active && plan.NextDate is not null).MinBy(plan => plan.NextDate);
        return language == "en" ? English(summary, next) : PolishText(summary, next);
    }

    private static string PolishText(BnplSummary summary, BnplPlan? next)
    {
        if (summary.ActivePlans == 0)
        {
            return "Nie masz aktywnych planów „kup teraz, zapłać później”. Pamiętaj, że BNPL to też dług: każda rata to stały koszt w kolejnych miesiącach.";
        }

        var plans = summary.ActivePlans switch
        {
            1 => "1 aktywny plan",
            var n when n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 => $"{n} aktywne plany",
            var n => $"{n} aktywnych planów",
        };
        var remaining = Money.ToZloty(summary.TotalRemainingGr).ToString("N2", Polish);
        var nextText = next is null
            ? ""
            : $" Najbliższa rata: {Money.ToZloty(next.InstalmentGr).ToString("N2", Polish)} zł ({next.Merchant}, {next.NextDate!.Value.ToString("dd.MM.yyyy", Polish)}).";
        return $"Masz {plans} „kup teraz, zapłać później” i do spłaty zostało {remaining} zł.{nextText} Każda rata to stały koszt w kolejnych miesiącach, więc zanim weźmiesz kolejną, sprawdź, czy zmieści się w budżecie.";
    }

    private static string English(BnplSummary summary, BnplPlan? next)
    {
        if (summary.ActivePlans == 0)
        {
            return "You have no active buy-now-pay-later plans. Remember that BNPL is still debt: every instalment is a fixed cost in the months ahead.";
        }

        var plans = summary.ActivePlans == 1 ? "1 active buy-now-pay-later plan" : $"{summary.ActivePlans} active buy-now-pay-later plans";
        var remaining = Money.ToZloty(summary.TotalRemainingGr).ToString("N2", CultureInfo.InvariantCulture);
        var nextText = next is null
            ? ""
            : $" Next instalment: {Money.ToZloty(next.InstalmentGr).ToString("N2", CultureInfo.InvariantCulture)} zł ({next.Merchant}, {next.NextDate!.Value:yyyy-MM-dd}).";
        return $"You have {plans} with {remaining} zł left to pay.{nextText} Every instalment is a fixed cost in the months ahead, so check it fits your budget before taking on another one.";
    }
}

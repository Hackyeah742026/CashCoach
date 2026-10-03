using System.Globalization;
using CashCoach.Core.Domain;

namespace CashCoach.Core.Analytics;

/// <summary>Deterministic Polish and English templates. They are the fallback whenever AI text is unavailable or fails the fact check.</summary>
public static class CoachTexts
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static bool IsEnglish(string language) => language == "en";

    /// <summary><c>1 234,50 zł</c> in Polish, <c>1,234.50 zł</c> in English.</summary>
    public static string Zl(long grosze, string language) =>
        Money.ToZloty(grosze).ToString("N2", IsEnglish(language) ? CultureInfo.InvariantCulture : Polish) + " zł";

    public static string Date(DateOnly date, string language) =>
        IsEnglish(language) ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : date.ToString("dd.MM.yyyy", Polish);

    public static string CategoryName(Category category, string language) => (category, IsEnglish(language)) switch
    {
        (Category.Groceries, false) => "zakupy spożywcze",
        (Category.Groceries, true) => "groceries",
        (Category.FoodDelivery, false) => "jedzenie z dostawą",
        (Category.FoodDelivery, true) => "food delivery",
        (Category.Restaurants, false) => "restauracje i kawiarnie",
        (Category.Restaurants, true) => "restaurants and cafés",
        (Category.Transport, false) => "transport",
        (Category.Transport, true) => "transport",
        (Category.Subscriptions, false) => "subskrypcje",
        (Category.Subscriptions, true) => "subscriptions",
        (Category.Shopping, false) => "zakupy",
        (Category.Shopping, true) => "shopping",
        (Category.Entertainment, false) => "rozrywka",
        (Category.Entertainment, true) => "entertainment",
        (Category.Rent, false) => "czynsz",
        (Category.Rent, true) => "rent",
        (Category.Utilities, false) => "rachunki",
        (Category.Utilities, true) => "bills",
        (Category.Health, false) => "zdrowie i uroda",
        (Category.Health, true) => "health and beauty",
        (Category.Education, false) => "edukacja",
        (Category.Education, true) => "education",
        (Category.Bnpl, false) => "raty BNPL",
        (Category.Bnpl, true) => "BNPL instalments",
        (Category.Transfers, false) => "przelewy",
        (Category.Transfers, true) => "transfers",
        (Category.Salary, false) => "wynagrodzenie",
        (Category.Salary, true) => "salary",
        (_, false) => "inne",
        (_, true) => "other",
    };

    /// <summary>Polish plural: <c>1 zamówienie</c>, <c>2 zamówienia</c>, <c>5 zamówień</c>.</summary>
    public static string PolishPlural(long n, string one, string few, string many) =>
        n == 1 ? one : n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? few : many;

    public static string WeekdayName(DayOfWeek day, string language) =>
        IsEnglish(language) ? day.ToString() : Polish.DateTimeFormat.GetDayName(day);

    // ---------------------------------------------------------------- opportunities

    public static string OpportunityTitle(Opportunity o, string language)
    {
        var en = IsEnglish(language);
        
        return o.Type switch
        {
            OpportunityType.FoodDelivery => en ? "Order delivery half as often" : "Zamawiaj jedzenie o połowę rzadziej",
            OpportunityType.DuplicateSubscription => en ? $"Keep one of: {o.Subject}" : $"Zostaw jedną z: {o.Subject}",
            OpportunityType.UnusedSubscription => en ? $"Cancel {o.Subject}" : $"Anuluj {o.Subject}",
            OpportunityType.SmallDailyBuys => en ? "Cut small daily buys by 30%" : "Ogranicz drobne zakupy o 30%",
            OpportunityType.TaxiRides => en ? "Swap half of taxi rides for public transport" : "Zamień połowę przejazdów taksówką na komunikację",
            OpportunityType.Bnpl => en ? "Pay off instalments, skip new ones" : "Spłać raty i nie bierz nowych",
            _ => o.Id,
        };
    }

    public static string OpportunityRationale(Opportunity o, string language)
    {
        var en = IsEnglish(language);
        var spend = Zl(o.MonthlySpendGr, language);
        var saving = Zl(o.MonthlySavingGr, language);
        return o.Type switch
        {
            OpportunityType.FoodDelivery => en
                ? $"You spend about {spend} a month on delivery ({o.Subject}). Half as often saves {saving} a month."
                : $"Na dostawy jedzenia wydajesz ok. {spend} miesięcznie ({o.Subject}). Zamawiając o połowę rzadziej, oszczędzisz {saving} miesięcznie.",
            OpportunityType.DuplicateSubscription => en
                ? $"These services overlap and cost {spend} a month together. Keeping one saves {saving} a month."
                : $"Te usługi się dublują i razem kosztują {spend} miesięcznie. Zostawiając jedną, oszczędzisz {saving} miesięcznie.",
            OpportunityType.UnusedSubscription => en
                ? $"You said you no longer use it. Cancelling saves {saving} a month."
                : $"Piszesz, że już z tego nie korzystasz. Anulowanie to {saving} miesięcznie.",
            OpportunityType.SmallDailyBuys => en
                ? $"About {o.Count} small buys a month ({o.Subject}) add up to {spend}. Cutting 30% saves {saving}."
                : $"Ok. {o.Count} {PolishPlural(o.Count, "drobny zakup", "drobne zakupy", "drobnych zakupów")} miesięcznie ({o.Subject}) to razem {spend}. Mniej o 30% to {saving} oszczędności.",
            OpportunityType.TaxiRides => en
                ? $"Taxi rides ({o.Subject}) cost about {spend} a month. Half of them by bus or tram saves {saving}."
                : $"Przejazdy ({o.Subject}) kosztują ok. {spend} miesięcznie. Połowa z nich komunikacją miejską to {saving} oszczędności.",
            OpportunityType.Bnpl => en
                ? $"Instalments ({o.Subject}) take {spend} a month. Once paid off, that money is yours again."
                : $"Raty ({o.Subject}) zabierają {spend} miesięcznie. Po spłacie te pieniądze znów są Twoje.",
            _ => "",
        };
    }

    // ---------------------------------------------------------------- alerts

    public static (string Title, string Message) AlertText(Alert alert, string language)
    {
        var en = IsEnglish(language);
        var amount = alert.AmountGr is { } gr ? Zl(gr, language) : "";
        var date = alert.Date is { } d ? Date(d, language) : "";
        return alert.Type switch
        {
            AlertType.RunOut => en
                ? ("Money may run out before payday", $"At this pace your balance goes below zero on {date}. You would be short {amount} by payday.")
                : ("Pieniądze mogą skończyć się przed wypłatą", $"W tym tempie saldo spadnie poniżej zera {date}. Do wypłaty zabraknie {amount}."),
            AlertType.Bnpl => en
                ? ("Instalment due soon", $"{alert.Subject}: {amount} on {date}.")
                : ("Zbliża się rata", $"{alert.Subject}: {amount}, termin {date}."),
            AlertType.DuplicateSub => en
                ? ("Overlapping subscriptions", $"{alert.Subject} do the same job. Keeping one saves {amount} a month.")
                : ("Dublujące się subskrypcje", $"{alert.Subject} robią to samo. Zostawiając jedną, oszczędzisz {amount} miesięcznie."),
            AlertType.Challenge => en
                ? ("Challenge streak at risk", $"\"{alert.Subject}\": a payment on {date} breaks the streak. Check in to start again.")
                : ("Wyzwanie zagrożone", $"„{alert.Subject}”: płatność z {date} przerywa serię. Zrób check-in i zacznij od nowa."),
            _ => ("", ""),
        };
    }

    // ---------------------------------------------------------------- challenges

    public static string ChallengeTitle(string type, long days, string language) => (type, IsEnglish(language)) switch
    {
        (ChallengeTypes.NoDelivery, false) => $"{days} dni bez jedzenia z dostawą",
        (ChallengeTypes.NoDelivery, true) => $"{days} days without food delivery",
        (ChallengeTypes.NoTaxi, false) => $"{days} dni bez taksówek",
        (ChallengeTypes.NoTaxi, true) => $"{days} days without taxis",
        _ => type,
    };

    // ---------------------------------------------------------------- wrapped

    public static (string Title, string Description) PersonalityText(Personality personality, string language) => (personality.Key, IsEnglish(language)) switch
    {
        ("instalment_juggler", false) => ("Żongler Rat", "Kilka planów BNPL naraz. Każda rata to stały koszt, więc warto je najpierw spłacić."),
        ("instalment_juggler", true) => ("Instalment Juggler", "Several BNPL plans at once. Every instalment is a fixed cost, so pay them off first."),
        ("delivery_lover", false) => ("Mistrz Dostaw", "Kurier zna Twój adres na pamięć. Gotowanie kilka razy w tygodniu szybko się zwraca."),
        ("delivery_lover", true) => ("Delivery Devotee", "The courier knows your address by heart. Cooking a few times a week pays off fast."),
        ("city_rider", false) => ("Miejski Jeździec", "Wygoda ma swoją cenę. Część przejazdów komunikacją to łatwa oszczędność."),
        ("city_rider", true) => ("City Rider", "Comfort has a price. Taking the tram for some rides is an easy saving."),
        ("bargain_hunter", false) => ("Łowca Okazji", "Zakupy to Twój żywioł. Lista „poczekam 48 h” pomaga odsiać impulsy."),
        ("bargain_hunter", true) => ("Bargain Hunter", "Shopping is your thing. A 48-hour wait list helps filter out impulse buys."),
        ("weekend_foodie", false) => ("Weekendowy Smakosz", "Najwięcej wydajesz w weekendy. Plan na sobotę pomaga trzymać budżet."),
        ("weekend_foodie", true) => ("Weekend Foodie", "Weekends are when you spend most. A Saturday plan keeps the budget on track."),
        (_, false) => ("Spokojny Planista", "Wydatki pod kontrolą. Tak trzymaj i odkładaj nadwyżki na cele."),
        (_, true) => ("Steady Planner", "Spending under control. Keep it up and move the surplus to your goals."),
    };

    public static string MonthName(DateOnly month, string language) => IsEnglish(language)
        ? month.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
        : Polish.DateTimeFormat.GetMonthName(month.Month) + " " + month.Year;

    public static string BiggestChangeCaption(WrappedStats s, string language)
    {
        if (s.BiggestChange is not { } c)
        {
            return CoachTexts.IsEnglish(language) ? "Your spending was steady this month." : "W tym miesiącu wydatki były stabilne.";
        }

        var name = CategoryName(c.Category, language);
        return IsEnglish(language)
            ? $"{name}: {Zl(c.FromGr, language)} → {Zl(c.ToGr, language)} ({(c.ChangePct >= 0 ? "+" : "−")}{Math.Abs(c.ChangePct)}%)."
            : $"{Capitalize(name)}: {Zl(c.FromGr, language)} → {Zl(c.ToGr, language)} ({(c.ChangePct >= 0 ? "+" : "−")}{Math.Abs(c.ChangePct)}%).";
    }

    public static string GoalPreviewText(GoalPreview preview, string language)
    {
        var en = IsEnglish(language);
        var week = Zl(preview.RequiredPerWeekGr, language);
        return preview.Verdict switch
        {
            Verdict.Green => en
                ? $"You need {week} a week. Your usual monthly surplus covers it."
                : $"Potrzebujesz {week} tygodniowo. Twoja zwykła miesięczna nadwyżka to pokrywa.",
            Verdict.Yellow => en
                ? $"You need {week} a week. It works if you follow the savings plan below."
                : $"Potrzebujesz {week} tygodniowo. Uda się, jeśli wprowadzisz poniższe oszczędności.",
            _ => en
                ? $"You need {week} a week, more than your budget allows now. Try a later deadline or a smaller target."
                : $"Potrzebujesz {week} tygodniowo, więcej niż pozwala teraz budżet. Spróbuj późniejszego terminu albo mniejszej kwoty.",
        };
    }

    private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0], Polish) + text[1..];

    public static (string Title, string Caption) WrappedCardText(string type, WrappedStats s, string language)
    {
        var en = IsEnglish(language);
        string Z(long gr) => Zl(gr, language);
        switch (type)
        {
            case WrappedCardTypes.TotalSpent:
                return en
                    ? ("Total spent", $"In {MonthName(s.Month, language)} you spent {Z(s.TotalSpentGr)} across {s.TransactionCount} {(s.TransactionCount == 1 ? "transaction" : "transactions")}.")
                    : ("Wydatki w sumie", $"{Capitalize(MonthName(s.Month, language))}: wydane {Z(s.TotalSpentGr)} w {s.TransactionCount} {PolishPlural(s.TransactionCount, "transakcji", "transakcjach", "transakcjach")}.");
            case WrappedCardTypes.TopCategories:
                var top = s.TopCategories.FirstOrDefault();
                return top is null
                    ? (en ? "Top categories" : "Top kategorie", en ? "No spending this month." : "Brak wydatków w tym miesiącu.")
                    : en
                        ? ("Top categories", $"Most went to {CategoryName(top.Category, language)}: {Z(top.AmountGr)} ({top.SharePct}% of spending).")
                        : ("Top kategorie", $"Najwięcej poszło na {CategoryName(top.Category, language)}: {Z(top.AmountGr)} ({top.SharePct}% wydatków).");
            case WrappedCardTypes.TopMerchant:
                return s.TopMerchant is not { } m
                    ? (en ? "Favourite place" : "Ulubione miejsce", en ? "No favourite place this month." : "W tym miesiącu bez ulubionego miejsca.")
                    : en
                        ? ("Favourite place", $"{m.Name}: {m.Count} visits, {Z(m.AmountGr)} in total.")
                        : ("Ulubione miejsce", $"{m.Name}: {m.Count} {PolishPlural(m.Count, "wizyta", "wizyty", "wizyt")}, razem {Z(m.AmountGr)}.");
            case WrappedCardTypes.Delivery:
                return en
                    ? ("Delivery", s.Delivery.Count == 0 ? "No food delivery this month. Nice!" : $"{s.Delivery.Count} deliveries for {Z(s.Delivery.AmountGr)}, that's about {s.Fun.Count} pizzas.")
                    : ("Dostawy", s.Delivery.Count == 0 ? "Zero dostaw w tym miesiącu. Brawo!" : $"{s.Delivery.Count} {PolishPlural(s.Delivery.Count, "zamówienie", "zamówienia", "zamówień")} za {Z(s.Delivery.AmountGr)}, czyli ok. {s.Fun.Count} {PolishPlural(s.Fun.Count, "pizza", "pizze", "pizz")}.");
            case WrappedCardTypes.BiggestDay:
                return s.BiggestDay is not { } d
                    ? (en ? "Biggest day" : "Najdroższy dzień", en ? "No spontaneous spending this month." : "Brak spontanicznych wydatków.")
                    : en
                        ? ("Biggest day", $"{Date(d.Date, language)}: {Z(d.AmountGr)} in {d.Count} payments.")
                        : ("Najdroższy dzień", $"{Date(d.Date, language)}: {Z(d.AmountGr)} w {d.Count} {PolishPlural(d.Count, "płatności", "płatnościach", "płatnościach")}.");
            case WrappedCardTypes.Subscriptions:
                return en
                    ? ("Subscriptions", $"{s.Subscriptions.Count} subscriptions cost {Z(s.Subscriptions.AmountGr)} this month.")
                    : ("Subskrypcje", $"Subskrypcje ({s.Subscriptions.Count}) kosztowały {Z(s.Subscriptions.AmountGr)} w tym miesiącu.");
            case WrappedCardTypes.MonthOverMonth:
                if (s.ChangePct is not { } pct)
                {
                    return en ? ("Month over month", "No previous month to compare yet.") : ("Miesiąc do miesiąca", "Brak poprzedniego miesiąca do porównania.");
                }

                var direction = pct >= 0 ? (en ? "more" : "więcej") : (en ? "less" : "mniej");
                return en
                    ? ("Month over month", $"{Math.Abs(pct)}% {direction} than last month ({Z(s.PreviousSpentGr)}).")
                    : ("Miesiąc do miesiąca", $"O {Math.Abs(pct)}% {direction} niż miesiąc wcześniej ({Z(s.PreviousSpentGr)}).");
            case WrappedCardTypes.Personality:
                var (title, description) = PersonalityText(s.Personality, language);
                return (title, description);
            default:
                return (type, "");
        }
    }
}

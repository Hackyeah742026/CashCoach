using Bogus;
using CashCoach.Core.Domain;

namespace CashCoach.Infrastructure.SyntheticData;

/// <summary>A fixed monthly payment or income. <see cref="Merchant"/> is the display name the pipeline should resolve it to.</summary>
public sealed record MonthlyEntry(string Merchant, Func<Randomizer, DateOnly, string> Description, long AmountGr, int Day);

/// <summary>A BNPL plan paid monthly from <see cref="FirstDate"/>, starting at instalment <see cref="FirstNumber"/>.</summary>
public sealed record BnplPlanSeed(string Provider, string Merchant, string DescriptionPrefix, long InstalmentGr, int Total, int FirstNumber, DateOnly FirstDate);

/// <summary>A day-to-day merchant with a random expense between <see cref="MinGr"/> and <see cref="MaxGr"/> grosze.</summary>
public sealed record NoiseMerchant(string Merchant, Func<Randomizer, string> Description, int MinGr, int MaxGr);

public sealed record SyntheticPersona(
    Persona Persona,
    int Seed,
    MonthlyEntry Salary,
    IReadOnlyList<MonthlyEntry> OtherIncome,
    MonthlyEntry Rent,
    IReadOnlyList<MonthlyEntry> Bills,
    IReadOnlyList<MonthlyEntry> Subscriptions,
    IReadOnlyList<BnplPlanSeed> BnplPlans,
    IReadOnlyList<(NoiseMerchant Merchant, float Weight)> Noise);

/// <summary>The planted facts for each demo persona; tests assert the pipeline finds them.</summary>
public static class SyntheticPersonas
{
    private static readonly string[] PolishMonths =
        ["STYCZEN", "LUTY", "MARZEC", "KWIECIEN", "MAJ", "CZERWIEC", "LIPIEC", "SIERPIEN", "WRZESIEN", "PAZDZIERNIK", "LISTOPAD", "GRUDZIEN"];

    private static class Noise
    {
        public static readonly NoiseMerchant Zabka = new("Żabka", r => $"ZABKA Z{r.ReplaceNumbers("####")} K.1 KRAKOW", 450, 3_500);
        public static readonly NoiseMerchant Biedronka = new("Biedronka", r => $"BIEDRONKA {r.ReplaceNumbers("####")} KRAKOW", 1_500, 16_000);
        public static readonly NoiseMerchant Lidl = new("Lidl", r => $"LIDL POLSKA {r.ReplaceNumbers("####")} KRAKOW", 2_000, 18_000);
        public static readonly NoiseMerchant Bolt = new("Bolt", r => $"BOLT.EU/O/{r.ReplaceNumbers("####")}", 1_100, 4_800);
        public static readonly NoiseMerchant Uber = new("Uber", _ => "UBER *TRIP HELP.UBER.COM", 1_400, 5_500);
        public static readonly NoiseMerchant Pyszne = new("Pyszne.pl", r => $"BLIK {r.ReplaceNumbers("#######")} PYSZNE.PL", 3_200, 8_500);
        public static readonly NoiseMerchant Glovo = new("Glovo", r => $"GLOVO*ORDER {r.ReplaceNumbers("######")} KRAKOW", 2_800, 7_500);
        public static readonly NoiseMerchant Wolt = new("Wolt", r => $"WOLT.COM/{r.ReplaceNumbers("######")} KRAKOW", 3_000, 8_000);
        public static readonly NoiseMerchant Allegro = new("Allegro", r => $"PAYU*ALLEGRO {r.ReplaceNumbers("########")}", 2_500, 26_000);
        public static readonly NoiseMerchant Rossmann = new("Rossmann", r => $"ROSSMANN {r.ReplaceNumbers("###")} KRAKOW", 1_200, 9_500);
        public static readonly NoiseMerchant McDonalds = new("McDonald's", r => $"MCDONALDS {r.ReplaceNumbers("####")} KRAKOW", 1_800, 4_500);
        public static readonly NoiseMerchant Starbucks = new("Starbucks", r => $"STARBUCKS {r.ReplaceNumbers("###")} KRAKOW", 1_400, 3_200);
        public static readonly NoiseMerchant Kawiarnia = new("Kawiarnia", _ => "SUMUP *KAWIARNIA BLOK", 900, 2_800);
        public static readonly NoiseMerchant Jakdojade = new("Jakdojade", _ => "JAKDOJADE.PL BILET", 340, 680);
        public static readonly NoiseMerchant CinemaCity = new("Cinema City", _ => "CINEMA CITY BONARKA KRAKOW", 2_500, 9_000);
        public static readonly NoiseMerchant Steam = new("Steam", r => $"STEAMGAMES.COM {r.ReplaceNumbers("##########")}", 1_500, 18_000);
        public static readonly NoiseMerchant Orlen = new("Orlen", r => $"ORLEN STACJA NR {r.ReplaceNumbers("####")} KRAKOW", 6_000, 25_000);
        public static readonly NoiseMerchant Shein = new("Shein", _ => "SHEIN.COM DUBLIN", 3_000, 20_000);
        public static readonly NoiseMerchant Temu = new("Temu", _ => "TEMU.COM DUBLIN", 1_500, 12_000);
        public static readonly NoiseMerchant Hebe = new("Hebe", r => $"HEBE {r.ReplaceNumbers("####")} KRAKOW", 1_500, 8_000);
        public static readonly NoiseMerchant Empik = new("Empik", r => $"EMPIK {r.ReplaceNumbers("####")} KRAKOW", 2_000, 12_000);
        public static readonly NoiseMerchant Apteka = new("Apteka", _ => "APTEKA GEMINI KRAKOW", 1_000, 7_000);
        public static readonly NoiseMerchant PhoneTransfer = new("Przelew na telefon", _ => "BLIK PRZELEW NA TELEFON", 2_000, 15_000);

        /// <summary>Deliberately absent from merchants.json, so it ends up as <c>other</c>.</summary>
        public static readonly NoiseMerchant Unknown = new("Phu Polmax", r => $"PHU POLMAX {r.ReplaceNumbers("####")} KRAKOW", 1_000, 6_000);
    }

    private static MonthlyEntry Spotify(int day) =>
        new("Spotify", (r, _) => $"SPOTIFY P{r.Replace("?#?#?#?#?#")} STOCKHOLM", -2_399, day);

    private static MonthlyEntry YouTubeMusic(int day) =>
        new("YouTube Music", (_, _) => "GOOGLE *YOUTUBE MUSIC G.CO/HELPPAY#", -2_399, day);

    private static MonthlyEntry Constant(string merchant, string description, long amountGr, int day) =>
        new(merchant, (_, _) => description, amountGr, day);

    private static string SalaryTitle(DateOnly date, string employer) =>
        $"PRZELEW PRZYCHODZACY WYNAGRODZENIE ZA {PolishMonths[date.Month - 1]} {employer}";

    public static readonly SyntheticPersona Student = new(
        Persona.Student,
        Seed: 2026_01,
        Salary: Constant("Stypendium", "PRZELEW PRZYCHODZACY STYPENDIUM SOCJALNE", 165_000, 10),
        OtherIncome: [Constant("Kieszonkowe", "PRZELEW PRZYCHODZACY KIESZONKOWE", 130_000, 1)],
        Rent: Constant("Czynsz", "PRZELEW CZYNSZ POKOJ", -110_000, 1),
        Bills: [],
        Subscriptions:
        [
            Spotify(6),
            YouTubeMusic(14),
            Constant("Netflix", "NETFLIX.COM AMSTERDAM", -3_300, 20),
            Constant("iCloud+", "APPLE.COM/BILL ICLOUD", -399, 3),
        ],
        BnplPlans: [new("PayPo", "Modivo", "PAYPO*MODIVO", 8_975, 4, 1, new DateOnly(2026, 8, 3))],
        Noise:
        [
            (Noise.Zabka, 16), (Noise.Biedronka, 5), (Noise.Lidl, 3), (Noise.Bolt, 3), (Noise.Uber, 1),
            (Noise.Pyszne, 3), (Noise.Glovo, 3), (Noise.Wolt, 1), (Noise.Allegro, 1), (Noise.Rossmann, 2),
            (Noise.McDonalds, 3), (Noise.Starbucks, 3), (Noise.Kawiarnia, 6), (Noise.Jakdojade, 12),
            (Noise.CinemaCity, 1), (Noise.Steam, 0.5f), (Noise.PhoneTransfer, 2), (Noise.Hebe, 1),
            (Noise.Apteka, 1), (Noise.Unknown, 1),
        ]);

    public static readonly SyntheticPersona FirstJob = new(
        Persona.FirstJob,
        Seed: 2026_02,
        Salary: new("Wynagrodzenie", (_, date) => SalaryTitle(date, "ACME SP Z O O"), 560_000, 28),
        OtherIncome: [],
        Rent: Constant("Czynsz", "PRZELEW CZYNSZ MIESZKANIE", -230_000, 1),
        Bills: [Constant("Play", "PLAY P4 SP Z O O FAKTURA", -5_500, 18)],
        Subscriptions:
        [
            Spotify(6),
            YouTubeMusic(14),
            Constant("Netflix", "NETFLIX.COM AMSTERDAM", -4_900, 20),
            Constant("ChatGPT", "OPENAI *CHATGPT SUBSCR", -8_900, 9),
        ],
        BnplPlans: [new("Klarna", "Zalando", "KLARNA*ZALANDO", 12_450, 4, 1, new DateOnly(2026, 7, 15))],
        Noise:
        [
            (Noise.Zabka, 8), (Noise.Biedronka, 6), (Noise.Lidl, 8), (Noise.Bolt, 6), (Noise.Uber, 5),
            (Noise.Pyszne, 5), (Noise.Glovo, 5), (Noise.Wolt, 5), (Noise.Allegro, 4), (Noise.Rossmann, 3),
            (Noise.McDonalds, 3), (Noise.Starbucks, 6), (Noise.Kawiarnia, 3), (Noise.Orlen, 2),
            (Noise.CinemaCity, 1.5f), (Noise.Empik, 1.5f), (Noise.PhoneTransfer, 3), (Noise.Hebe, 1),
            (Noise.Apteka, 1), (Noise.Unknown, 2),
        ]);

    public static readonly SyntheticPersona BnplHeavy = new(
        Persona.BnplHeavy,
        Seed: 2026_03,
        Salary: new("Wynagrodzenie", (_, date) => SalaryTitle(date, "SKLEP24 SP Z O O"), 430_000, 15),
        OtherIncome: [],
        Rent: Constant("Czynsz", "PRZELEW CZYNSZ WYNAJEM", -135_000, 1),
        Bills: [],
        Subscriptions:
        [
            Spotify(6),
            YouTubeMusic(14),
            Constant("Disney+", "DISNEYPLUS AMSTERDAM", -3_799, 22),
        ],
        BnplPlans:
        [
            new("Klarna", "Zalando", "KLARNA*ZALANDO", 9_999, 6, 2, new DateOnly(2026, 7, 7)),
            new("PayPo", "Modivo", "PAYPO*MODIVO", 14_900, 3, 1, new DateOnly(2026, 8, 20)),
            new("Twisto", "Media Expert", "TWISTO*MEDIA EXPERT", 33_300, 6, 1, new DateOnly(2026, 9, 12)),
        ],
        Noise:
        [
            (Noise.Zabka, 16), (Noise.Biedronka, 4), (Noise.Lidl, 3), (Noise.Bolt, 4), (Noise.Uber, 2),
            (Noise.Pyszne, 4), (Noise.Glovo, 4), (Noise.Wolt, 2), (Noise.Allegro, 1.5f), (Noise.Rossmann, 2),
            (Noise.McDonalds, 4), (Noise.Starbucks, 3), (Noise.Kawiarnia, 4), (Noise.Shein, 1), (Noise.Temu, 1.5f),
            (Noise.Hebe, 2), (Noise.Empik, 1), (Noise.PhoneTransfer, 1), (Noise.Apteka, 1), (Noise.Unknown, 1),
        ]);

    public static IReadOnlyList<SyntheticPersona> All { get; } = [Student, FirstJob, BnplHeavy];

    public static SyntheticPersona For(Persona persona) => All.Single(p => p.Persona == persona);
}

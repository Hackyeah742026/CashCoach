using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CashCoach.Core.Domain;

namespace CashCoach.Core.Services;

/// <param name="MerchantKey">Up to two meaningful uppercase ASCII tokens, e.g. <c>ZABKA</c> or <c>YOUTUBE MUSIC</c>.</param>
/// <param name="BnplProvider">Klarna, PayPo or Twisto when named in the description.</param>
/// <param name="InstalmentNumber">The <c>n</c> of an <c>n/m</c> instalment marker such as <c>RATA 2/4</c>.</param>
/// <param name="InstalmentCount">The <c>m</c> of an <c>n/m</c> instalment marker.</param>
public sealed record NormalizedDescription(
    string MerchantKey,
    Channel Channel,
    bool IsBnpl,
    string? BnplProvider,
    int? InstalmentNumber,
    int? InstalmentCount);

/// <summary>Turns a messy bank description into a stable merchant key, payment channel and BNPL markers.</summary>
public static partial class MerchantNormalizer
{
    public const string UnknownMerchantKey = "UNKNOWN";
    private const int MaxKeyTokens = 2;
    private const int MaxInstalments = 48;

    private static readonly Dictionary<string, string> BnplProviders = new()
    {
        ["KLARNA"] = "Klarna",
        ["PAYPO"] = "PayPo",
        ["TWISTO"] = "Twisto",
    };

    private static readonly HashSet<string> NoiseTokens =
    [
        // Payment rails, BNPL markers and bank boilerplate.
        "BLIK", "KLARNA", "PAYPO", "TWISTO", "RATA", "RATY", "RAT", "PRZELEW", "PRZYCHODZACY", "WYCHODZACY",
        "ZAKUP", "PRZY", "UZYCIU", "KARTA", "KARTY", "KARTE", "PLATNOSC", "TRANSAKCJA", "BEZGOTOWKOWA", "OPERACJA",
        "NR", "UL", "WWW", "HTTP", "HTTPS",
        // Domains, countries and legal forms.
        "PL", "COM", "EU", "NET", "ORG", "IO", "CO", "SE", "IE", "NL", "LU", "DE", "GB", "UK", "US", "SP", "SA",
        "POLSKA", "POLAND",
        // Polish stopwords that would otherwise leak personal names into the key.
        "DO", "OD", "NA", "ZA", "DLA",
        // Cities.
        "KRAKOW", "WARSZAWA", "WROCLAW", "POZNAN", "GDANSK", "GDYNIA", "SOPOT", "LODZ", "KATOWICE", "LUBLIN",
        "SZCZECIN", "BYDGOSZCZ", "TORUN", "RZESZOW", "BIALYSTOK", "OLSZTYN", "KIELCE", "OPOLE", "CZESTOCHOWA",
        "RADOM", "GLIWICE", "STOCKHOLM", "AMSTERDAM", "DUBLIN", "LUXEMBOURG", "LONDON", "BERLIN",
        // Months, so "WYNAGRODZENIE ZA LIPIEC" and "... ZA SIERPIEN" share a key.
        "STYCZEN", "LUTY", "MARZEC", "KWIECIEN", "MAJ", "CZERWIEC", "LIPIEC", "SIERPIEN", "WRZESIEN",
        "PAZDZIERNIK", "LISTOPAD", "GRUDZIEN",
    ];

    public static NormalizedDescription Normalize(string rawDescription)
    {
        var text = DatePattern().Replace(StripDiacritics(rawDescription.ToUpperInvariant()), " ");

        var channel = DetectChannel(text);
        var provider = BnplProviderPattern().Match(text) is { Success: true } providerMatch
            ? BnplProviders[providerMatch.Value]
            : null;
        var (instalmentNumber, instalmentCount) = FindInstalment(text);
        var isBnpl = provider is not null || instalmentCount is not null || BnplKeywordPattern().IsMatch(text);

        text = InstalmentPattern().Replace(text, " ");
        text = PaymentPrefixPattern().Replace(text, " ");
        text = text.Replace("APPLE.COM/BILL", " ", StringComparison.Ordinal);
        text = CardReferencePattern().Replace(text, " ");

        var tokens = TokenSeparator().Split(text)
            .Select(token => token.Trim('-'))
            .Where(IsMeaningful)
            .Take(MaxKeyTokens)
            .ToList();

        return new NormalizedDescription(
            tokens.Count > 0 ? string.Join(' ', tokens) : UnknownMerchantKey,
            channel,
            isBnpl,
            provider,
            instalmentNumber,
            instalmentCount);
    }

    /// <summary>BLIK when the description mentions BLIK, transfer when it says PRZELEW, otherwise a card payment.</summary>
    private static Channel DetectChannel(string text) =>
        BlikPattern().IsMatch(text) ? Channel.Blik
        : TransferPattern().IsMatch(text) ? Channel.Transfer
        : Channel.Card;

    private static (int? Number, int? Count) FindInstalment(string text)
    {
        foreach (Match match in InstalmentPattern().Matches(text))
        {
            var number = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
            var count = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
            if (number >= 1 && count >= 2 && number <= count && count <= MaxInstalments)
            {
                return (number, count);
            }
        }

        return (null, null);
    }

    private static bool IsMeaningful(string token) =>
        token.Length >= 2 && !token.Any(char.IsAsciiDigit) && !NoiseTokens.Contains(token);

    private static string StripDiacritics(string text)
    {
        // Ł has no Unicode decomposition, so it needs an explicit mapping.
        var decomposed = text.Replace('Ł', 'L').Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}[./-]\d{1,2}[./-]\d{2,4}\b")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"\b(KLARNA|PAYPO|TWISTO)\b")]
    private static partial Regex BnplProviderPattern();

    [GeneratedRegex(@"\b(RATA|RATY|RAT)\b")]
    private static partial Regex BnplKeywordPattern();

    [GeneratedRegex(@"(?<!\d)(?<n>\d{1,2})\s*/\s*(?<m>\d{1,2})(?!\d)")]
    private static partial Regex InstalmentPattern();

    [GeneratedRegex(@"\b(PAYU|PAYPRO|SUMUP|KLARNA|PAYPO|TWISTO|GOOGLE|PAYPAL)\s*\*")]
    private static partial Regex PaymentPrefixPattern();

    [GeneratedRegex(@"\bK\.\s*\d+\b")]
    private static partial Regex CardReferencePattern();

    [GeneratedRegex(@"\bBLIK\b")]
    private static partial Regex BlikPattern();

    [GeneratedRegex(@"\bPRZELEW\b")]
    private static partial Regex TransferPattern();

    [GeneratedRegex(@"[^A-Z0-9\-]+")]
    private static partial Regex TokenSeparator();
}

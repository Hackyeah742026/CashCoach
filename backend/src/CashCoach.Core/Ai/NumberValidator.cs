using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CashCoach.Core.Ai;

/// <param name="Unverified">Numbers in the text that match no computed fact, as written.</param>
public sealed record NumberCheck(bool Passed, IReadOnlyList<string> Unverified);

/// <summary>
/// Checks that every number in an AI answer is one of the computed facts the model was given.
/// Understands Polish and English formats (<c>1 234,50</c>, <c>1,234.50</c>, <c>1234.5</c>, <c>30%</c>).
/// Small counts (0 to 10), years and dates are ignored.
/// </summary>
public static partial class NumberValidator
{
    public const decimal MoneyTolerance = 0.01m;

    /// <summary>A whole number in the text may be a fact rounded to whole złoty.</summary>
    public const decimal RoundedTolerance = 1m;

    public static NumberCheck Validate(string text, IEnumerable<decimal> facts)
    {
        var allowed = facts.Select(Math.Abs).Distinct().ToList();
        var unverified = new List<string>();
        foreach (var (raw, candidates, isWhole) in Extract(text))
        {
            // "3 120 zł" may be "3" and "120 zł" rather than 3120.
            var parts = SpaceLike().Split(raw);
            if (!IsVerified(candidates, isWhole, allowed)
                && !(parts.Length > 1 && parts.All(part => Extract(part).All(p => IsVerified(p.Candidates, p.IsWhole, allowed)))))
            {
                unverified.Add(raw);
            }
        }

        return new NumberCheck(unverified.Count == 0, unverified);
    }

    private static bool IsVerified(IReadOnlyList<decimal> candidates, bool isWhole, List<decimal> allowed)
    {
        var tolerance = isWhole ? RoundedTolerance : MoneyTolerance;
        return candidates.All(IsIgnored) || candidates.Any(value => allowed.Any(fact => Math.Abs(value - fact) <= tolerance));
    }

    /// <summary>Every number in a JSON document, plus day, month and year of ISO dates in strings.</summary>
    public static IReadOnlyList<decimal> Flatten(string json)
    {
        var numbers = new List<decimal>();
        using var document = JsonDocument.Parse(json);
        Walk(document.RootElement, numbers);
        return numbers;
    }

    private static void Walk(JsonElement element, List<decimal> numbers)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                numbers.Add(element.GetDecimal());
                break;
            case JsonValueKind.String:
                foreach (Match match in IsoDate().Matches(element.GetString() ?? ""))
                {
                    numbers.Add(int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture));
                    numbers.Add(int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture));
                    numbers.Add(int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture));
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, numbers);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, numbers);
                }

                break;
        }
    }

    /// <returns>Each number as written, its possible values (an ambiguous <c>1,234</c> has two) and whether it has no decimals.</returns>
    public static IEnumerable<(string Raw, IReadOnlyList<decimal> Candidates, bool IsWhole)> Extract(string text)
    {
        var cleaned = DatesAndTimes().Replace(text, " ");
        foreach (Match match in NumberPattern().Matches(cleaned))
        {
            var raw = match.Value.Trim();
            var digits = raw.TrimEnd('%').Replace('−', '-').Replace("-", "");
            var candidates = new List<decimal>();
            bool isWhole;

            if (PolishThousands().IsMatch(digits))
            {
                // 1 234,50
                var normalized = SpaceLike().Replace(digits, "").Replace(',', '.');
                candidates.Add(Parse(normalized));
                isWhole = !normalized.Contains('.');
            }
            else if (EnglishThousands().IsMatch(digits))
            {
                // 1,234.50 or (Polish decimal) 1,234
                candidates.Add(Parse(digits.Replace(",", "")));
                if (!digits.Contains('.') && digits.Count(c => c == ',') == 1)
                {
                    candidates.Add(Parse(digits.Replace(',', '.')));
                }

                isWhole = !digits.Contains('.');
            }
            else
            {
                var normalized = digits.Replace(',', '.');
                candidates.Add(Parse(normalized));
                isWhole = !normalized.Contains('.');
            }

            yield return (raw, candidates, isWhole);
        }
    }

    private static bool IsIgnored(decimal value) =>
        value == decimal.Truncate(value) && (value is >= 0 and <= 10 || value is >= 1900 and <= 2100);

    private static decimal Parse(string value) => decimal.Parse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"(?<y>\d{4})-(?<m>\d{2})-(?<d>\d{2})")]
    private static partial Regex IsoDate();

    /// <summary>ISO dates, dd.MM.yyyy, times, "15 października", "October 15", and YYYY-MM month keys.</summary>
    [GeneratedRegex(
        @"\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}[./]\d{1,2}[./]\d{2,4}\b|\b\d{4}-\d{2}\b|\b\d{1,2}:\d{2}\b" +
        @"|\b\d{1,2}\.?\s+(?:stycznia|lutego|marca|kwietnia|maja|czerwca|lipca|sierpnia|września|wrzesnia|października|pazdziernika|listopada|grudnia|january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)\b" +
        @"|\b(?:january|february|march|april|may|june|july|august|september|october|november|december|jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)\.?\s+\d{1,2}(?:st|nd|rd|th)?\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex DatesAndTimes();

    [GeneratedRegex(@"(?<![\p{L}\d.,])[-−]?(?:\d{1,3}(?:[   ]\d{3})+(?:,\d{1,2})?|\d{1,3}(?:,\d{3})+(?:\.\d+)?|\d+(?:[.,]\d+)?)%?(?![\p{L}\d])")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^\d{1,3}(?:[   ]\d{3})+(?:,\d{1,2})?$")]
    private static partial Regex PolishThousands();

    [GeneratedRegex(@"^\d{1,3}(?:,\d{3})+(?:\.\d+)?$")]
    private static partial Regex EnglishThousands();

    [GeneratedRegex(@"[   ]")]
    private static partial Regex SpaceLike();
}

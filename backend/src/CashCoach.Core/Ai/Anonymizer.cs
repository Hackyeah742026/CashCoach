using System.Text.RegularExpressions;

namespace CashCoach.Core.Ai;

/// <summary>Removes account numbers, card numbers, phone numbers and e-mail addresses before text is sent to the model.</summary>
public static partial class Anonymizer
{
    public static string Scrub(string text)
    {
        text = Iban().Replace(text, "[konto]");
        text = Email().Replace(text, "[email]");
        text = Phone().Replace(text, "[telefon]");
        return LongDigits().Replace(text, "[numer]");
    }

    [GeneratedRegex(@"\b[A-Z]{2}\s?\d{2}(?:\s?\d{4}){4,7}\b|\b\d{26}\b", RegexOptions.IgnoreCase)]
    private static partial Regex Iban();

    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.]+")]
    private static partial Regex Email();

    [GeneratedRegex(@"(?:\+48[\s-]?)?\b\d{3}[\s-]\d{3}[\s-]\d{3}\b")]
    private static partial Regex Phone();

    /// <summary>Card numbers and other runs of 9 or more digits (amounts are shorter).</summary>
    [GeneratedRegex(@"\b\d[\d ]{7,}\d\b")]
    private static partial Regex LongDigits();
}

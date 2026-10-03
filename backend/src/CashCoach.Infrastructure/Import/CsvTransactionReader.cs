using System.Globalization;
using System.Text;
using CashCoach.Core.Domain;
using CsvHelper;
using CsvHelper.Configuration;

namespace CashCoach.Infrastructure.Import;

public sealed record CsvTransactionRow(int LineNumber, DateOnly Date, long AmountGr, string Description);

/// <summary>A CSV that cannot be imported; the message is safe to show to the user.</summary>
public sealed class ImportFormatException(string message) : Exception(message);

/// <summary>Reads <c>date;amount;description;currency</c> CSVs. Amounts may use a decimal comma or dot.</summary>
public static class CsvTransactionReader
{
    public const string Header = "date;amount;description;currency";
    public const string Currency = "PLN";

    private const decimal MaxAbsoluteAmount = 10_000_000m;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd.MM.yyyy", "dd-MM-yyyy"];

    public static CsvConfiguration Configuration => new(CultureInfo.InvariantCulture)
    {
        Delimiter = ";",
        NewLine = "\n",
        PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),
        TrimOptions = TrimOptions.Trim,
        MissingFieldFound = null,
    };

    public static IReadOnlyList<CsvTransactionRow> Read(Stream stream)
    {
        try
        {
            return ReadRows(stream);
        }
        catch (CsvHelperException)
        {
            throw new ImportFormatException($"The file is not a valid '{Header}' CSV.");
        }
    }

    private static List<CsvTransactionRow> ReadRows(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csv = new CsvReader(reader, Configuration);

        if (!csv.Read() || !csv.ReadHeader())
        {
            throw new ImportFormatException("The file is empty.");
        }

        var header = csv.HeaderRecord ?? [];
        foreach (var required in new[] { "date", "amount", "description" })
        {
            if (!header.Any(column => column.Trim().Equals(required, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ImportFormatException($"The header must be '{Header}'; the '{required}' column is missing.");
            }
        }

        var hasCurrency = header.Any(column => column.Trim().Equals("currency", StringComparison.OrdinalIgnoreCase));
        var rows = new List<CsvTransactionRow>();
        while (csv.Read())
        {
            var line = csv.Parser.RawRow;
            var date = ParseDate(csv.GetField("date"), line);
            var amountGr = ParseAmount(csv.GetField("amount"), line);
            var description = csv.GetField("description")?.Trim();
            if (string.IsNullOrEmpty(description))
            {
                throw new ImportFormatException($"Line {line}: the description is empty.");
            }

            var currency = hasCurrency ? csv.GetField("currency") : null;
            if (!string.IsNullOrWhiteSpace(currency) && !currency.Equals(Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new ImportFormatException($"Line {line}: only {Currency} is supported, got '{currency}'.");
            }

            rows.Add(new CsvTransactionRow(line, date, amountGr, description));
        }

        return rows;
    }

    /// <summary>Formats grosze the way Polish banks export them, with a decimal comma.</summary>
    public static string FormatAmount(long amountGr) =>
        Money.ToZloty(amountGr).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static DateOnly ParseDate(string? value, int line) =>
        DateOnly.TryParseExact(value?.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ImportFormatException($"Line {line}: '{value}' is not a date (use YYYY-MM-DD).");

    private static long ParseAmount(string? value, int line)
    {
        var text = (value ?? "").Replace(" ", "").Replace("\u00A0", "");
        if (text.Contains(',') && text.Contains('.'))
        {
            // "1.234,56": the dot groups thousands and the comma is the decimal separator.
            text = text.Replace(".", "");
        }

        text = text.Replace(',', '.');
        if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var zloty)
            || decimal.Round(zloty, 2) != zloty
            || Math.Abs(zloty) > MaxAbsoluteAmount)
        {
            throw new ImportFormatException($"Line {line}: '{value}' is not an amount in złoty.");
        }

        return (long)(zloty * 100);
    }
}

using System.Text;
using Bogus;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Import;
using CsvHelper;

namespace CashCoach.Infrastructure.SyntheticData;

public sealed record SyntheticTransaction(DateOnly Date, long AmountGr, string Description);

/// <summary>Deterministic (fixed-seed) synthetic bank history for the demo personas. Contains no real data.</summary>
public static class SyntheticDataGenerator
{
    public static readonly DateOnly PeriodStart = new(2026, 7, 1);
    public static readonly DateOnly PeriodEnd = new(2026, 9, 30);

    /// <summary>Probability of 0, 1, 2 or 3 everyday purchases on a given day.</summary>
    private static readonly int[] DailyPurchaseCounts = [0, 1, 2, 3];
    private static readonly float[] DailyPurchaseWeights = [0.2f, 0.5f, 0.2f, 0.1f];

    public static IReadOnlyList<SyntheticTransaction> Generate(Persona persona)
    {
        var profile = SyntheticPersonas.For(persona);
        var random = new Randomizer(profile.Seed);
        var rows = new List<SyntheticTransaction>();

        var monthly = new[] { profile.Salary, profile.Rent }
            .Concat(profile.OtherIncome)
            .Concat(profile.Bills)
            .Concat(profile.Subscriptions);
        for (var month = PeriodStart; month <= PeriodEnd; month = month.AddMonths(1))
        {
            foreach (var entry in monthly)
            {
                var date = new DateOnly(month.Year, month.Month, entry.Day);
                rows.Add(new SyntheticTransaction(date, entry.AmountGr, entry.Description(random, date)));
            }
        }

        foreach (var plan in profile.BnplPlans)
        {
            for (int number = plan.FirstNumber, offset = 0; number <= plan.Total; number++, offset++)
            {
                var date = plan.FirstDate.AddMonths(offset);
                if (date > PeriodEnd)
                {
                    break;
                }

                rows.Add(new SyntheticTransaction(date, -plan.InstalmentGr, $"{plan.DescriptionPrefix} RATA {number}/{plan.Total}"));
            }
        }

        var merchants = profile.Noise.Select(noise => noise.Merchant).ToArray();
        // Bogus expects weights that sum to 1.
        var totalWeight = profile.Noise.Sum(noise => noise.Weight);
        var weights = profile.Noise.Select(noise => noise.Weight / totalWeight).ToArray();
        for (var day = PeriodStart; day <= PeriodEnd; day = day.AddDays(1))
        {
            var purchases = random.WeightedRandom(DailyPurchaseCounts, DailyPurchaseWeights);
            for (var i = 0; i < purchases; i++)
            {
                var merchant = random.WeightedRandom(merchants, weights);
                rows.Add(new SyntheticTransaction(day, -random.Number(merchant.MinGr, merchant.MaxGr), merchant.Description(random)));
            }
        }

        return rows.OrderBy(row => row.Date).ToList();
    }

    /// <summary>The persona's history as a <c>date;amount;description;currency</c> CSV with Polish decimal commas.</summary>
    public static string GenerateCsv(Persona persona)
    {
        using var writer = new StringWriter();
        using (var csv = new CsvWriter(writer, CsvTransactionReader.Configuration))
        {
            foreach (var column in CsvTransactionReader.Header.Split(';'))
            {
                csv.WriteField(column);
            }

            csv.NextRecord();
            foreach (var row in Generate(persona))
            {
                csv.WriteField(row.Date.ToString("yyyy-MM-dd"));
                csv.WriteField(CsvTransactionReader.FormatAmount(row.AmountGr));
                csv.WriteField(row.Description);
                csv.WriteField(CsvTransactionReader.Currency);
                csv.NextRecord();
            }
        }

        return writer.ToString();
    }

    public static Stream GenerateCsvStream(Persona persona) => new MemoryStream(Encoding.UTF8.GetBytes(GenerateCsv(persona)));
}

using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Persistence;
using CashCoach.Infrastructure.Recurring;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Import;

/// <summary>Imported transactions per categorization step. User rules and BNPL instalments count as <see cref="Dictionary"/>.</summary>
public sealed record CategorizationCounts(int Dictionary, int Fuzzy, int Llm, int Other);

/// <param name="Period">First and last date in the file, or <c>null</c> for a file without rows.</param>
public sealed record ImportResult(
    int Imported,
    int SkippedDuplicates,
    CategorizationCounts Categorized,
    RecurringSyncResult Recurring,
    DateRange? Period);

/// <summary>CSV, then normalize, categorize and detect recurring payments for one user.</summary>
public sealed class TransactionImportService(AppDbContext db, Categorizer categorizer, RecurringSyncService recurringSync)
{
    /// <exception cref="ImportFormatException">The CSV is malformed.</exception>
    public async Task<ImportResult> ImportAsync(Guid userId, Stream csv, CancellationToken cancellationToken)
    {
        var rows = CsvTransactionReader.Read(csv);

        var seen = (await db.Transactions
                .Where(t => t.UserId == userId)
                .Select(t => new { t.Date, t.AmountGr, t.RawDescription })
                .ToListAsync(cancellationToken))
            .Select(t => (t.Date, t.AmountGr, t.RawDescription))
            .ToHashSet();
        var fresh = new List<(CsvTransactionRow Row, NormalizedDescription Normalized)>();
        foreach (var row in rows)
        {
            if (!seen.Add((row.Date, row.AmountGr, row.Description)))
            {
                continue;
            }

            fresh.Add((row, MerchantNormalizer.Normalize(row.Description)));
        }

        var matches = await categorizer.CategorizeAsync(fresh.Select(item => item.Normalized.MerchantKey), cancellationToken);
        var rules = await db.UserMerchantRules
            .Where(r => r.UserId == userId)
            .ToDictionaryAsync(r => r.Merchant, r => r.Category, cancellationToken);

        var counts = new Dictionary<CategorySource, int>();
        foreach (var (row, normalized) in fresh)
        {
            var match = matches[normalized.MerchantKey];
            var (category, source) = rules.TryGetValue(match.Name, out var ruleCategory) ? (ruleCategory, CategorySource.Dictionary)
                : normalized.InstalmentCount is not null ? (Category.Bnpl, CategorySource.Dictionary)
                : (match.Category, match.Source);
            counts[source] = counts.GetValueOrDefault(source) + 1;

            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Date = row.Date,
                AmountGr = row.AmountGr,
                RawDescription = row.Description,
                Merchant = match.Name,
                Category = category,
                Channel = normalized.Channel,
                IsBnpl = normalized.IsBnpl,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        var recurring = await recurringSync.RefreshAsync(userId, cancellationToken);

        return new ImportResult(
            fresh.Count,
            rows.Count - fresh.Count,
            new CategorizationCounts(
                counts.GetValueOrDefault(CategorySource.Dictionary),
                counts.GetValueOrDefault(CategorySource.Fuzzy),
                counts.GetValueOrDefault(CategorySource.Llm),
                counts.GetValueOrDefault(CategorySource.Other)),
            recurring,
            rows.Count == 0 ? null : new DateRange(rows.Min(row => row.Date), rows.Max(row => row.Date)));
    }
}

using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Persistence;
using CashCoach.Infrastructure.Recurring;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Import;

/// <summary>Imported transactions per categorization step. User rules and BNPL instalments count as <see cref="Dictionary"/>.</summary>
public sealed record CategorizationCounts(int Dictionary, int Fuzzy, int Llm, int Other);

/// <param name="Period">First and last date in the file, or <c>null</c> for a file without rows.</param>
/// <param name="DetectedBalanceGr">Balance after the newest row, when the file has a balance column.</param>
public sealed record ImportResult(
    int Imported,
    int SkippedDuplicates,
    CategorizationCounts Categorized,
    RecurringSyncResult Recurring,
    DateRange? Period,
    long? DetectedBalanceGr = null);

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

        // Transfer titles can carry personal names, so their keys never leave the server.
        var transferKeys = fresh.Where(item => item.Normalized.Channel == Channel.Transfer).Select(item => item.Normalized.MerchantKey).ToHashSet();
        var matches = await categorizer.CategorizeAsync(fresh.Select(item => item.Normalized.MerchantKey), cancellationToken, transferKeys);
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
        var detectedBalance = NewestBalance(rows);
        await UpdateUserAsync(userId, detectedBalance, cancellationToken);

        return new ImportResult(
            fresh.Count,
            rows.Count - fresh.Count,
            new CategorizationCounts(
                counts.GetValueOrDefault(CategorySource.Dictionary),
                counts.GetValueOrDefault(CategorySource.Fuzzy),
                counts.GetValueOrDefault(CategorySource.Llm),
                counts.GetValueOrDefault(CategorySource.Other)),
            recurring,
            rows.Count == 0 ? null : new DateRange(rows.Min(row => row.Date), rows.Max(row => row.Date)),
            detectedBalance);
    }

    /// <summary>
    /// The balance column of the newest row. Banks export newest-first or oldest-first, so among rows of the newest date
    /// the first one wins in a descending file and the last one in an ascending file.
    /// </summary>
    public static long? NewestBalance(IReadOnlyList<CsvTransactionRow> rows)
    {
        var withBalance = rows.Where(row => row.BalanceGr is not null).ToList();
        if (withBalance.Count == 0)
        {
            return null;
        }

        var newest = withBalance.Max(row => row.Date);
        var latest = withBalance.Where(row => row.Date == newest).ToList();
        var descending = withBalance[0].Date >= withBalance[^1].Date && withBalance[0].Date != withBalance[^1].Date;
        return (descending ? latest[0] : latest[^1]).BalanceGr;
    }

    /// <summary>A balance from the file replaces the estimate; the payday guess follows the data until the user confirms their income.</summary>
    private async Task UpdateUserAsync(Guid userId, long? detectedBalance, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return;
        }

        user.BalanceGr = detectedBalance ?? user.BalanceGr;
        if (user.IncomeStatus == IncomeStatus.Unknown)
        {
            var transactions = await db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.AmountGr > 0).ToListAsync(cancellationToken);
            if (IncomeDetector.Detect(transactions).FirstOrDefault() is { } guess)
            {
                user.Payday = guess.Day;
                user.PaydayRule = guess.DayRule;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

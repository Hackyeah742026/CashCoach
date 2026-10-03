using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Transactions;

/// <param name="Query">Case-insensitive text matched against the merchant and the raw description.</param>
/// <param name="Ids">Only these transactions (for evidence), or <c>null</c> for all.</param>
public sealed record TransactionFilter(DateOnly? From, DateOnly? To, Category? Category, string? Query, int Limit, int Offset, IReadOnlyList<Guid>? Ids = null);

/// <param name="Total">Number of transactions matching the filter, before paging.</param>
/// <param name="SumGr">Signed sum of all matching transactions, before paging.</param>
public sealed record TransactionPage(int Total, long SumGr, IReadOnlyList<Transaction> Items);

public sealed class TransactionService(AppDbContext db)
{
    public async Task<TransactionPage> ListAsync(Guid userId, TransactionFilter filter, CancellationToken cancellationToken)
    {
        var query = db.Transactions.AsNoTracking().Where(t => t.UserId == userId);
        if (filter.From is { } from)
        {
            query = query.Where(t => t.Date >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(t => t.Date <= to);
        }

        if (filter.Category is { } category)
        {
            query = query.Where(t => t.Category == category);
        }

        if (filter.Ids is { } ids)
        {
            query = query.Where(t => ids.Contains(t.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var text = filter.Query.Trim().ToLower();
            query = query.Where(t => t.Merchant.ToLower().Contains(text) || t.RawDescription.ToLower().Contains(text));
        }

        var total = await query.CountAsync(cancellationToken);
        var sum = await query.SumAsync(t => t.AmountGr, cancellationToken);
        var items = await query
            .OrderByDescending(t => t.Date)
            .ThenBy(t => t.Id)
            .Skip(filter.Offset)
            .Take(filter.Limit)
            .ToListAsync(cancellationToken);

        return new TransactionPage(total, sum, items);
    }

    /// <summary>
    /// Sets the category of one transaction, or with <paramref name="applyToMerchant"/> of all the user's transactions
    /// from the same merchant and remembers it as a rule for future imports.
    /// </summary>
    /// <returns>The number of updated transactions, or <c>null</c> when the user has no such transaction.</returns>
    public async Task<int?> UpdateCategoryAsync(
        Guid userId, Guid transactionId, Category category, bool applyToMerchant, CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions.SingleOrDefaultAsync(t => t.Id == transactionId && t.UserId == userId, cancellationToken);
        if (transaction is null)
        {
            return null;
        }

        if (!applyToMerchant)
        {
            transaction.Category = category;
            await db.SaveChangesAsync(cancellationToken);
            return 1;
        }

        var rule = await db.UserMerchantRules.SingleOrDefaultAsync(r => r.UserId == userId && r.Merchant == transaction.Merchant, cancellationToken);
        if (rule is null)
        {
            db.UserMerchantRules.Add(new UserMerchantRule { Id = Guid.NewGuid(), UserId = userId, Merchant = transaction.Merchant, Category = category });
        }
        else
        {
            rule.Category = category;
        }

        var sameMerchant = await db.Transactions
            .Where(t => t.UserId == userId && t.Merchant == transaction.Merchant)
            .ToListAsync(cancellationToken);
        foreach (var match in sameMerchant)
        {
            match.Category = category;
        }

        await db.SaveChangesAsync(cancellationToken);
        return sameMerchant.Count;
    }
}

using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Transactions;

namespace CashCoach.Api.Contracts;

/// <param name="Amount">Złoty; expenses negative, income positive.</param>
public sealed record TransactionDto(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Merchant,
    Category Category,
    string RawDescription,
    Channel Channel,
    bool IsRecurring,
    bool IsBnpl)
{
    public static TransactionDto From(Transaction transaction) => new(
        transaction.Id,
        transaction.Date,
        Money.ToZloty(transaction.AmountGr),
        transaction.Merchant,
        transaction.Category,
        transaction.RawDescription,
        transaction.Channel,
        transaction.IsRecurring,
        transaction.IsBnpl);
}

/// <param name="Total">Number of transactions matching the filter (not a money sum).</param>
/// <param name="Sum">Signed złoty sum of all matching transactions (before paging).</param>
public sealed record TransactionListResponse(int Total, decimal Sum, IReadOnlyList<TransactionDto> Items)
{
    public static TransactionListResponse From(TransactionPage page) =>
        new(page.Total, Money.ToZloty(page.SumGr), page.Items.Select(TransactionDto.From).ToList());
}

public sealed record UpdateTransactionCategoryRequest(string? Category, bool? ApplyToMerchant);

public sealed record UpdateTransactionCategoryResponse(Guid Id, Category Category, int UpdatedCount);

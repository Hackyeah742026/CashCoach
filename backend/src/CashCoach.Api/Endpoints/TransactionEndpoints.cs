using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Transactions;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class TransactionEndpoints
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 500;

    public static RouteGroupBuilder MapTransactionEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/transactions", async (
                DateOnly? from, DateOnly? to, string? category, string? q, string? ids, int? limit, int? offset,
                CurrentUser currentUser, TransactionService transactions, CancellationToken cancellationToken) =>
            {
                var pageSize = limit ?? DefaultLimit;
                if (pageSize is < 1 or > MaxLimit)
                {
                    throw BadRequest("invalid_limit", $"'limit' must be between 1 and {MaxLimit}.");
                }

                if (offset is < 0)
                {
                    throw BadRequest("invalid_offset", "'offset' must not be negative.");
                }

                if (from > to)
                {
                    throw BadRequest("invalid_range", "'from' must not be after 'to'.");
                }

                Category? categoryFilter = category is null ? null : ParseEnum<Category>(category, "category");
                List<Guid>? idFilter = null;
                if (ids is not null)
                {
                    idFilter = [];
                    foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        idFilter.Add(Guid.TryParse(part, out var id) ? id : throw BadRequest("invalid_ids", "'ids' must be comma-separated transaction ids."));
                    }
                }

                var filter = new TransactionFilter(from, to, categoryFilter, q, pageSize, offset ?? 0, idFilter);
                return TypedResults.Ok(TransactionListResponse.From(await transactions.ListAsync(currentUser.Id, filter, cancellationToken)));
            })
            .WithName("ListTransactions")
            .WithTags("Transactions");

        api.MapPatch("/transactions/{id:guid}", async (
                Guid id, UpdateTransactionCategoryRequest request,
                CurrentUser currentUser, TransactionService transactions, CancellationToken cancellationToken) =>
            {
                var category = ParseEnum<Category>(request.Category, "category");
                var updated = await transactions.UpdateCategoryAsync(currentUser.Id, id, category, request.ApplyToMerchant ?? false, cancellationToken)
                    ?? throw NotFound("transaction_not_found", "Transaction not found.");
                return TypedResults.Ok(new UpdateTransactionCategoryResponse(id, category, updated));
            })
            .WithName("UpdateTransactionCategory")
            .WithTags("Transactions");

        return api;
    }
}

using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Infrastructure.Import;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class ImportEndpoints
{
    private const long MaxFileBytes = 5 * 1024 * 1024;

    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/import", async (IFormFile file, CurrentUser currentUser, TransactionImportService importer, CancellationToken cancellationToken) =>
            {
                if (file.Length == 0)
                {
                    throw BadRequest("invalid_csv", "The file is empty.");
                }

                if (file.Length > MaxFileBytes)
                {
                    throw BadRequest("file_too_large", "The file must be at most 5 MB.");
                }

                try
                {
                    await using var stream = file.OpenReadStream();
                    var result = await importer.ImportAsync(currentUser.Id, stream, cancellationToken);
                    return TypedResults.Ok(ImportResponse.From(result));
                }
                catch (ImportFormatException exception)
                {
                    throw BadRequest("invalid_csv", exception.Message);
                }
            })
            // Authentication is the X-User-Id header, not a cookie, so there is no cross-site form post to protect against.
            .DisableAntiforgery()
            .WithName("ImportTransactions")
            .WithTags("Transactions");

        return api;
    }
}

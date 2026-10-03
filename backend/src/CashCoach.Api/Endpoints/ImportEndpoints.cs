using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Import;
using CashCoach.Infrastructure.Users;
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

        // "Try it with demo data": loads a persona's synthetic history into the current user.
        api.MapPost("/import/demo", async (DemoImportRequest? request, CurrentUser currentUser, DemoLoginService demo, CancellationToken cancellationToken) =>
            {
                var persona = request?.Persona is null ? Persona.BnplHeavy : ParseEnum<Persona>(request.Persona, "persona");
                if (persona == Persona.Custom)
                {
                    throw BadRequest("invalid_persona", "'persona' must be one of: student, first_job, bnpl_heavy.");
                }

                return TypedResults.Ok(ImportResponse.From(await demo.ImportDemoAsync(currentUser.Id, persona, cancellationToken)));
            })
            .WithName("ImportDemoData")
            .WithTags("Transactions");

        return api;
    }
}

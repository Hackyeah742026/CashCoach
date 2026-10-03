using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Users;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class UserEndpoints
{
    private const int MaxNameLength = 50;
    private static readonly string[] Languages = ["pl", "en"];
    private static readonly string[] ConsentScopes = ["transactions", "ai_coach"];

    /// <summary><c>POST /demo/login</c>; does not require <c>X-User-Id</c>.</summary>
    public static RouteGroupBuilder MapDemoEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/demo/login", async (DemoLoginRequest request, DemoLoginService demo, CancellationToken cancellationToken) =>
            {
                var persona = ParseEnum<Persona>(request.Persona, "persona");
                if (persona == Persona.Custom)
                {
                    throw BadRequest("invalid_persona", "'persona' must be one of: student, first_job, bnpl_heavy.");
                }

                return TypedResults.Ok(UserProfileResponse.From(await demo.LoginAsync(persona, cancellationToken)));
            })
            .WithName("DemoLogin")
            .WithTags("Users");

        api.MapPost("/users", async (CreateUserRequest request, DemoLoginService users, CancellationToken cancellationToken) =>
            {
                var language = request.Language ?? "pl";
                if (!Languages.Contains(language))
                {
                    throw BadRequest("invalid_language", $"'language' must be one of: {string.Join(", ", Languages)}.");
                }

                if (request.Name?.Trim() is { Length: > MaxNameLength })
                {
                    throw BadRequest("invalid_name", $"'name' must be at most {MaxNameLength} characters.");
                }

                var profile = await users.CreateUserAsync(request.Name, language, cancellationToken);
                return TypedResults.Created("/api/me", UserProfileResponse.From(profile));
            })
            .WithName("CreateUser")
            .WithTags("Users");

        return api;
    }

    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", async (CurrentUser currentUser, UserProfileService profiles, CancellationToken cancellationToken) =>
                TypedResults.Ok(UserProfileResponse.From(await profiles.GetAsync(currentUser.User, cancellationToken))))
            .WithName("GetMe")
            .WithTags("Users");

        api.MapPatch("/me", async (UpdateProfileRequest request, CurrentUser currentUser, UserProfileService profiles, CancellationToken cancellationToken) =>
            {
                var name = request.Name?.Trim();
                if (name is not null && (name.Length == 0 || name.Length > MaxNameLength))
                {
                    throw BadRequest("invalid_name", $"'name' must be 1 to {MaxNameLength} characters.");
                }

                if (request.Language is not null && !Languages.Contains(request.Language))
                {
                    throw BadRequest("invalid_language", $"'language' must be one of: {string.Join(", ", Languages)}.");
                }

                if (request.Payday is < 1 or > 31)
                {
                    throw BadRequest("invalid_payday", "'payday' must be a day of month from 1 to 31.");
                }

                long? buffer = request.SafetyBuffer is null ? null : ToGrosze(request.SafetyBuffer, "safety_buffer", 0m, 100_000m);
                long? balance = request.Balance is null ? null : ToGrosze(request.Balance, "balance", -10_000_000m);
                var update = new ProfileUpdate(name, request.Language, request.Payday, buffer, balance);
                var profile = await profiles.UpdateAsync(currentUser.User, update, cancellationToken);
                return TypedResults.Ok(UserProfileResponse.From(profile));
            })
            .WithName("UpdateMe")
            .WithTags("Users");

        api.MapPost("/me/consent", async (ConsentRequest request, CurrentUser currentUser, UserProfileService profiles, CancellationToken cancellationToken) =>
            {
                if (request.Accepted != true)
                {
                    throw BadRequest("consent_not_accepted", "Consent must be accepted to continue.");
                }

                var scopes = request.Scopes is { Count: > 0 } requested ? requested.Distinct().ToList() : [.. ConsentScopes];
                if (scopes.FirstOrDefault(scope => !ConsentScopes.Contains(scope)) is { } unknown)
                {
                    throw BadRequest("invalid_scope", $"Unknown consent scope '{unknown}'. Allowed: {string.Join(", ", ConsentScopes)}.");
                }

                var consentAt = await profiles.AcceptConsentAsync(currentUser.User, cancellationToken);
                return TypedResults.Ok(new ConsentResponse(new DateTimeOffset(consentAt, TimeSpan.Zero), scopes));
            })
            .WithName("AcceptConsent")
            .WithTags("Users");

        api.MapDelete("/me", async (CurrentUser currentUser, UserDataService data, CancellationToken cancellationToken) =>
            {
                await data.DeleteAsync(currentUser.Id, cancellationToken);
                return TypedResults.NoContent();
            })
            .WithName("DeleteMe")
            .WithTags("Users");

        api.MapGet("/me/export", async (HttpContext http, CurrentUser currentUser, UserDataService data, CancellationToken cancellationToken) =>
            {
                http.Response.Headers.ContentDisposition = $"attachment; filename=\"cashcoach-export-{currentUser.Id}.json\"";
                return TypedResults.Ok(await data.ExportAsync(currentUser.User, cancellationToken));
            })
            .WithName("ExportMe")
            .WithTags("Users");

        return api;
    }
}

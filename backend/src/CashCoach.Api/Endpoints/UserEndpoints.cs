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
                return TypedResults.Ok(UserProfileResponse.From(await demo.LoginAsync(persona, cancellationToken)));
            })
            .WithName("DemoLogin")
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

                var profile = await profiles.UpdateAsync(currentUser.User, name, request.Language, cancellationToken);
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

        return api;
    }
}

using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Users;

namespace CashCoach.Api.Contracts;

/// <param name="Persona"><c>student</c>, <c>first_job</c> or <c>bnpl_heavy</c>.</param>
public sealed record DemoLoginRequest(string? Persona);

public sealed record UserProfileResponse(Guid UserId, string Name, Persona Persona, string Language, bool HasConsent, bool HasData)
{
    public static UserProfileResponse From(UserProfile profile) => new(
        profile.User.Id,
        profile.User.Name,
        profile.User.Persona,
        profile.User.Language,
        profile.User.ConsentAt is not null,
        profile.HasData);
}

/// <param name="Language"><c>pl</c> or <c>en</c>; omit to keep the current one.</param>
public sealed record UpdateProfileRequest(string? Name, string? Language);

public sealed record ConsentRequest(bool? Accepted, IReadOnlyList<string>? Scopes);

public sealed record ConsentResponse(DateTimeOffset ConsentAt, IReadOnlyList<string> Scopes);

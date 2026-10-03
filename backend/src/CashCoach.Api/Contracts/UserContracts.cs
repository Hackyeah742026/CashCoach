using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Users;

namespace CashCoach.Api.Contracts;

/// <param name="Persona"><c>student</c>, <c>first_job</c> or <c>bnpl_heavy</c>.</param>
public sealed record DemoLoginRequest(string? Persona);

/// <param name="Payday">Day of month the salary arrives; detected on import, editable.</param>
/// <param name="SafetyBuffer">Złoty the forecast keeps untouched.</param>
/// <param name="Balance">Current balance in złoty: the value set by the user, or an estimate from the imported history.</param>
/// <param name="BalanceIsEstimate">True when the user has not set a balance yet.</param>
/// <param name="AsOf">Latest transaction date, the "today" the analytics use; <c>null</c> without data.</param>
/// <param name="AvailableMonths">Months with data, oldest first, as <c>YYYY-MM</c>.</param>
public sealed record UserProfileResponse(
    Guid UserId,
    string Name,
    Persona Persona,
    string Language,
    bool HasConsent,
    bool HasData,
    int? Payday,
    decimal SafetyBuffer,
    decimal Balance,
    bool BalanceIsEstimate,
    DateOnly? AsOf,
    IReadOnlyList<string> AvailableMonths)
{
    public static UserProfileResponse From(UserProfile profile) => new(
        profile.User.Id,
        profile.User.Name,
        profile.User.Persona,
        profile.User.Language,
        profile.User.ConsentAt is not null,
        profile.HasData,
        profile.User.Payday,
        Money.ToZloty(profile.User.SafetyBufferGr),
        Money.ToZloty(profile.BalanceGr),
        profile.User.BalanceGr is null,
        profile.AsOf,
        profile.AvailableMonths.Select(month => $"{month:yyyy-MM}").ToList());
}

/// <param name="Language"><c>pl</c> or <c>en</c>; omit to keep the current one.</param>
/// <param name="Payday">1 to 31.</param>
/// <param name="SafetyBuffer">Złoty, 0 or more.</param>
/// <param name="Balance">Current balance in złoty (may be negative).</param>
public sealed record UpdateProfileRequest(string? Name, string? Language, int? Payday, decimal? SafetyBuffer, decimal? Balance);

public sealed record ConsentRequest(bool? Accepted, IReadOnlyList<string>? Scopes);

public sealed record ConsentResponse(DateTimeOffset ConsentAt, IReadOnlyList<string> Scopes);

/// <param name="Language"><c>pl</c> (default) or <c>en</c>.</param>
public sealed record CreateUserRequest(string? Name, string? Language);

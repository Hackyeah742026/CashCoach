using CashCoach.Core.Domain;

namespace CashCoach.Api.Users;

/// <summary>The user resolved from the <c>X-User-Id</c> header for the current request.</summary>
public sealed class CurrentUser
{
    private User? _user;

    public User User => _user
        ?? throw new InvalidOperationException("The current user is not resolved. Add RequireCurrentUser() to the endpoint group.");

    public Guid Id => User.Id;

    internal void Set(User user) => _user = user;
}

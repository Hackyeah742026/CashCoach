using CashCoach.Api.Errors;
using CashCoach.Infrastructure.Persistence;

namespace CashCoach.Api.Users;

/// <summary>Resolves the <c>X-User-Id</c> header into <see cref="CurrentUser"/> or rejects the request.</summary>
internal sealed class CurrentUserFilter : IEndpointFilter
{
    public const string HeaderName = "X-User-Id";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var header = httpContext.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            throw new ApiException("missing_user_id", "The X-User-Id header is required.", StatusCodes.Status401Unauthorized);
        }

        if (!Guid.TryParse(header, out var userId))
        {
            throw new ApiException("invalid_user_id", "The X-User-Id header must be a valid user id.", StatusCodes.Status400BadRequest);
        }

        var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync([userId], httpContext.RequestAborted)
            ?? throw new ApiException("user_not_found", "No user exists for the given X-User-Id.", StatusCodes.Status404NotFound);

        httpContext.RequestServices.GetRequiredService<CurrentUser>().Set(user);
        return await next(context);
    }
}

public static class CurrentUserEndpointExtensions
{
    /// <summary>Requires a valid <c>X-User-Id</c> header on every endpoint in the group and exposes it as <see cref="CurrentUser"/>.</summary>
    public static RouteGroupBuilder RequireCurrentUser(this RouteGroupBuilder group) =>
        group.AddEndpointFilter<CurrentUserFilter>();
}

using CashCoach.Api.Contracts;
using Microsoft.AspNetCore.Diagnostics;

namespace CashCoach.Api.Errors;

internal sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, code, message) = exception switch
        {
            ApiException api => (api.StatusCode, api.Code, api.Message),
            BadHttpRequestException badRequest => (badRequest.StatusCode, "bad_request", "The request is malformed."),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "Something went wrong. Please try again."),
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse(new ErrorDetail(code, message)), cancellationToken);
        return true;
    }
}

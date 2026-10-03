namespace CashCoach.Api.Errors;

/// <summary>An expected client-facing error, rendered as <c>{ "error": { "code", "message" } }</c> with <see cref="StatusCode"/>.</summary>
public sealed class ApiException(string code, string message, int statusCode) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

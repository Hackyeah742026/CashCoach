using CashCoach.Api.Errors;
using CashCoach.Core.Domain;

namespace CashCoach.Api.Endpoints;

/// <summary>Parses snake_case enum values from requests into 400 errors that list the allowed values.</summary>
internal static class RequestValidation
{
    public static TEnum ParseEnum<TEnum>(string? value, string field) where TEnum : struct, Enum =>
        SnakeCaseEnum<TEnum>.TryParse(value, out var parsed)
            ? parsed
            : throw new ApiException(
                $"invalid_{field}",
                $"'{field}' must be one of: {string.Join(", ", SnakeCaseEnum<TEnum>.AllNames)}.",
                StatusCodes.Status400BadRequest);

    public static ApiException BadRequest(string code, string message) => new(code, message, StatusCodes.Status400BadRequest);

    public static ApiException NotFound(string code, string message) => new(code, message, StatusCodes.Status404NotFound);
}

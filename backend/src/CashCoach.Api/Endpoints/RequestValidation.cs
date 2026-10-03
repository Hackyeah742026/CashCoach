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

    /// <summary>Złoty with at most 2 decimals to grosze.</summary>
    public static long ToGrosze(decimal? zloty, string field, decimal min = decimal.MinValue, decimal max = 10_000_000m)
    {
        if (zloty is not { } value || decimal.Round(value, 2) != value || value < min || value > max)
        {
            throw BadRequest($"invalid_{field}", $"'{field}' must be an amount in złoty with at most 2 decimals, from {min:0.##} to {max:0.##}.");
        }

        return (long)(value * 100);
    }

    public static ApiException BadRequest(string code, string message) => new(code, message, StatusCodes.Status400BadRequest);

    public static ApiException NotFound(string code, string message) => new(code, message, StatusCodes.Status404NotFound);
}

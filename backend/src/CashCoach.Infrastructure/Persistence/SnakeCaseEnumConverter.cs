using CashCoach.Core.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CashCoach.Infrastructure.Persistence;

/// <summary>Stores enums as snake_case strings, matching their JSON form (e.g. <c>FoodDelivery</c> as <c>food_delivery</c>).</summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => SnakeCaseEnum<TEnum>.ToName(value),
    name => Parse(name))
    where TEnum : struct, Enum
{
    private static TEnum Parse(string name) => SnakeCaseEnum<TEnum>.TryParse(name, out var value)
        ? value
        : throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} value '{name}' in the database.");
}

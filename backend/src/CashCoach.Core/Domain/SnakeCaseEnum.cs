using System.Text.Json;

namespace CashCoach.Core.Domain;

/// <summary>snake_case names for enums, matching their JSON and database form (e.g. <c>FoodDelivery</c> as <c>food_delivery</c>).</summary>
public static class SnakeCaseEnum<TEnum> where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Names = Enum.GetValues<TEnum>()
        .ToDictionary(value => value, value => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString()));

    private static readonly Dictionary<string, TEnum> Values = Names.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static IReadOnlyCollection<string> AllNames => Values.Keys;

    public static string ToName(TEnum value) => Names[value];

    public static bool TryParse(string? name, out TEnum value)
    {
        value = default;
        return name is not null && Values.TryGetValue(name, out value);
    }
}

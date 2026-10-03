using CashCoach.Core.Ai;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class NumberValidatorTests
{
    private static readonly decimal[] Facts = [1234.50m, 412.30m, -280.97m, 30m, 24m, 14m, 2100m, 163.57m, 0.5m];

    [Theory]
    [InlineData("Na koncie masz 1 234,50 zł.")]
    [InlineData("Na koncie masz 1 234,50 zł.")]
    [InlineData("You have 1,234.50 zł.")]
    [InlineData("Na dostawy wydałeś 412,30 zł.")]
    [InlineData("Na dostawy wydałeś ok. 412 zł.")]
    [InlineData("Bezpieczna kwota to -280,97 zł.")]
    [InlineData("Brakuje Ci 280,97 zł.")]
    [InlineData("To 30% Twoich wydatków.")]
    [InlineData("Subskrypcja kosztuje 24.")]
    [InlineData("Masz 3 subskrypcje i 14 zamówień.")]
    [InlineData("Wypłata 15 października 2026, czyli za 15 dni? Nie: 15.10.2026.")]
    [InlineData("Payday is on October 15, 2026-10-15.")]
    [InlineData("Saldo 2100 zł, a do 3 120 zł brakuje dużo.")]
    [InlineData("Glovo: 163,57 zł w 3 zamówieniach.")]
    [InlineData("Nie mogę polecać kredytów ani kart kredytowych.")]
    public void Answers_with_only_computed_numbers_pass(string answer) =>
        NumberValidator.Validate(answer, Facts.Append(3m).Append(120m)).Passed.Should().BeTrue();

    [Theory]
    [InlineData("Na koncie masz 1 500,00 zł.", "1 500,00")]
    [InlineData("Wydałeś 412,99 zł na dostawy.", "412,99")]
    [InlineData("To 35% Twoich wydatków.", "35%")]
    [InlineData("Zaoszczędzisz 1,250.00 zł rocznie.", "1,250.00")]
    [InlineData("W tym tempie zostanie Ci 87 zł.", "87")]
    [InlineData("Masz 14 zamówień za 450 zł.", "450")]
    public void Invented_numbers_fail(string answer, string unverified)
    {
        var check = NumberValidator.Validate(answer, Facts);

        check.Passed.Should().BeFalse();
        check.Unverified.Should().Contain(unverified);
    }

    [Fact]
    public void Rounding_tolerance_applies_only_to_whole_numbers()
    {
        NumberValidator.Validate("ok. 413 zł", [412.30m]).Passed.Should().BeTrue();
        NumberValidator.Validate("412,40 zł", [412.30m]).Passed.Should().BeFalse();
    }

    [Fact]
    public void Flatten_collects_numbers_and_iso_date_parts_from_tool_json()
    {
        var numbers = NumberValidator.Flatten("""{"a":1.5,"b":[{"c":-2}],"d":"2026-10-15","e":"text 7"}""");

        numbers.Should().BeEquivalentTo([1.5m, -2m, 2026m, 10m, 15m]);
    }
}

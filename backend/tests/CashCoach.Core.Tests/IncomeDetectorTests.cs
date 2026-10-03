using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class IncomeDetectorTests
{
    private static Transaction Income(string date, decimal zloty, string merchant = "Wynagrodzenie", string? raw = null) => new()
    {
        Id = Guid.NewGuid(),
        Date = DateOnly.Parse(date),
        AmountGr = (long)(zloty * 100),
        Merchant = merchant,
        RawDescription = raw ?? $"PRZELEW PRZYCHODZACY {merchant.ToUpperInvariant()}",
        Category = Category.Other,
    };

    [Fact]
    public void Salary_on_the_same_day_every_month_is_a_high_confidence_guess()
    {
        var guess = IncomeDetector.Detect([Income("2026-07-10", 4500m), Income("2026-08-10", 4500m), Income("2026-09-10", 4500m)]).Single();

        guess.Should().BeEquivalentTo(new { Day = 10, DayRule = PaydayRule.FixedDay, AmountGr = 450_000L, MonthsSeen = 3, Kind = IncomeKind.Salary, Confidence = DetectionConfidence.High });
        guess.TransactionIds.Should().HaveCount(3);
    }

    [Fact]
    public void A_payday_on_a_weekend_paid_on_the_friday_before_still_matches()
    {
        // 2026-08-15 is a Saturday.
        var guess = IncomeDetector.Detect([Income("2026-07-15", 4300m), Income("2026-08-14", 4300m), Income("2026-09-15", 4300m)]).Single();

        guess.Day.Should().Be(15);
        guess.Confidence.Should().Be(DetectionConfidence.High);
    }

    [Fact]
    public void A_bonus_month_keeps_the_median_and_lowers_the_confidence()
    {
        var guess = IncomeDetector.Detect([Income("2026-07-28", 5000m), Income("2026-08-28", 9000m), Income("2026-09-28", 5000m)]).Single();

        guess.AmountGr.Should().Be(500_000);
        guess.MaxGr.Should().Be(900_000);
        guess.Confidence.Should().Be(DetectionConfidence.Medium);
    }

    [Fact]
    public void Payments_on_the_last_working_day_are_recognised()
    {
        // Jul 31 Fri, Aug 31 Mon, Sep 30 Wed.
        var guess = IncomeDetector.Detect([Income("2026-07-31", 6000m), Income("2026-08-31", 6000m), Income("2026-09-30", 6000m)]).Single();

        guess.DayRule.Should().Be(PaydayRule.LastWorkingDay);
        guess.Day.Should().Be(31);
    }

    [Fact]
    public void Several_incomes_are_ranked_largest_first_with_their_kind()
    {
        var candidates = IncomeDetector.Detect(
        [
            Income("2026-07-10", 1650m, "Stypendium", "PRZELEW PRZYCHODZACY STYPENDIUM SOCJALNE"),
            Income("2026-08-10", 1650m, "Stypendium", "PRZELEW PRZYCHODZACY STYPENDIUM SOCJALNE"),
            Income("2026-07-01", 900m, "Kieszonkowe"),
            Income("2026-08-01", 900m, "Kieszonkowe"),
        ]);

        candidates.Select(c => (c.Source, c.Kind, c.Day)).Should().Equal(("Stypendium", IncomeKind.Stipend, 10), ("Kieszonkowe", IncomeKind.Other, 1));
        candidates[0].Confidence.Should().Be(DetectionConfidence.Medium, "two matching months");
    }

    [Fact]
    public void Irregular_or_too_short_histories_give_no_guess()
    {
        IncomeDetector.Detect([Income("2026-07-03", 1200m, "Zlecenie"), Income("2026-08-20", 300m, "Zlecenie"), Income("2026-09-11", 2500m, "Zlecenie")])
            .Should().BeEmpty();
        IncomeDetector.Detect([Income("2026-09-10", 4500m)]).Should().BeEmpty();
        IncomeDetector.Detect([Income("2026-07-10", 20m, "Zwrot"), Income("2026-08-10", 20m, "Zwrot")]).Should().BeEmpty("small refunds are not income");
    }

    [Fact]
    public void Forecast_uses_the_last_working_day_rule()
    {
        // 2026-10-31 is a Saturday.
        ForecastCalculator.NextPayday(new DateOnly(2026, 9, 30), 31, PaydayRule.LastWorkingDay).Should().Be(new DateOnly(2026, 10, 30));
        ForecastCalculator.NextPayday(new DateOnly(2026, 9, 29), 31, PaydayRule.LastWorkingDay).Should().Be(new DateOnly(2026, 9, 30));
    }
}

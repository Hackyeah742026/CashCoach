using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class RecurringDetectorTests
{
    private static Transaction Tx(string date, long amountGr, string merchant, Category category, string? raw = null, bool isBnpl = false) => new()
    {
        Id = Guid.NewGuid(),
        Date = DateOnly.Parse(date),
        AmountGr = amountGr,
        Merchant = merchant,
        Category = category,
        RawDescription = raw ?? merchant.ToUpperInvariant(),
        IsBnpl = isBnpl,
    };

    private static IEnumerable<Transaction> Monthly(string merchant, Category category, long amountGr, int day, params int[] months) =>
        months.Select(month => Tx($"2026-{month:D2}-{day:D2}", amountGr, merchant, category));

    [Fact]
    public void Monthly_subscription_with_stable_amount_is_detected_with_next_date()
    {
        var result = RecurringDetector.Detect(Monthly("Spotify", Category.Subscriptions, -2_399, 6, 7, 8, 9).ToList());

        var spotify = result.Groups.Should().ContainSingle().Subject;
        spotify.Type.Should().Be(RecurringType.Subscription);
        spotify.AvgAmountGr.Should().Be(-2_399);
        spotify.PeriodDays.Should().Be(30);
        spotify.NextDate.Should().Be(new DateOnly(2026, 10, 6));
        spotify.Active.Should().BeTrue();
    }

    [Fact]
    public void Weekly_payments_are_detected()
    {
        var transactions = new[] { "2026-09-01", "2026-09-08", "2026-09-15" }
            .Select(date => Tx(date, -1_500, "Kino", Category.Entertainment)).ToList();

        var group = RecurringDetector.Detect(transactions).Groups.Should().ContainSingle().Subject;

        group.PeriodDays.Should().Be(7);
        group.NextDate.Should().Be(new DateOnly(2026, 9, 22));
    }

    [Fact]
    public void Irregular_intervals_or_amounts_varying_more_than_10_percent_are_not_recurring()
    {
        var transactions = new List<Transaction>
        {
            Tx("2026-07-01", -2_000, "Allegro", Category.Shopping),
            Tx("2026-07-12", -2_000, "Allegro", Category.Shopping),
            Tx("2026-07-05", -3_000, "Steam", Category.Entertainment),
            Tx("2026-08-05", -4_000, "Steam", Category.Entertainment),
        };

        RecurringDetector.Detect(transactions).Groups.Should().BeEmpty();
    }

    [Fact]
    public void Largest_monthly_rent_outflow_is_rent_and_largest_monthly_income_sets_the_salary_day()
    {
        var transactions = Monthly("Czynsz", Category.Rent, -110_000, 1, 7, 8, 9)
            .Concat(Monthly("Play", Category.Utilities, -5_500, 18, 7, 8, 9))
            .Concat(Monthly("Stypendium", Category.Salary, 165_000, 10, 7, 8, 9))
            .Concat(Monthly("Kieszonkowe", Category.Transfers, 90_000, 1, 7, 8, 9))
            .ToList();

        var result = RecurringDetector.Detect(transactions);

        result.SalaryDay.Should().Be(10);
        result.Groups.Should().ContainSingle(g => g.Type == RecurringType.Rent).Which.Merchant.Should().Be("Czynsz");
        var salary = result.Groups.Should().ContainSingle(g => g.Type == RecurringType.Salary).Subject;
        salary.Merchant.Should().Be("Stypendium");
        salary.NextDate.Should().Be(new DateOnly(2026, 10, 10));
        result.Groups.Should().Contain(g => g.Merchant == "Play" && g.Type == null);
        result.Groups.Should().Contain(g => g.Merchant == "Kieszonkowe" && g.Type == null);
    }

    [Fact]
    public void Series_that_stopped_is_inactive()
    {
        var transactions = Monthly("Netflix", Category.Subscriptions, -3_300, 20, 5, 6)
            .Append(Tx("2026-09-30", -1_000, "Żabka", Category.Groceries))
            .ToList();

        RecurringDetector.Detect(transactions).Groups.Single().Active.Should().BeFalse();
    }

    [Fact]
    public void Bnpl_instalments_become_plans_with_remaining_count_and_next_date()
    {
        var transactions = new List<Transaction>
        {
            Tx("2026-07-15", -12_450, "Zalando", Category.Bnpl, "KLARNA*ZALANDO RATA 1/4", isBnpl: true),
            Tx("2026-08-15", -12_450, "Zalando", Category.Bnpl, "KLARNA*ZALANDO RATA 2/4", isBnpl: true),
            Tx("2026-09-15", -12_450, "Zalando", Category.Bnpl, "KLARNA*ZALANDO RATA 3/4", isBnpl: true),
            Tx("2026-09-12", -33_300, "Media Expert", Category.Bnpl, "TWISTO*MEDIA EXPERT RATA 1/6", isBnpl: true),
        };

        var plans = BnplPlanBuilder.Build(transactions);

        plans.Should().HaveCount(2);
        var zalando = plans.Single(p => p.Merchant == "Zalando");
        zalando.Should().BeEquivalentTo(new { Provider = "Klarna", InstalmentGr = 12_450L, Paid = 3, Total = 4, RemainingInstalments = 1, RemainingGr = 12_450L, NextDate = (DateOnly?)new DateOnly(2026, 10, 15) });
        var mediaExpert = plans.Single(p => p.Merchant == "Media Expert");
        mediaExpert.Should().BeEquivalentTo(new { Provider = "Twisto", Paid = 1, Total = 6, RemainingGr = 166_500L, NextDate = (DateOnly?)new DateOnly(2026, 10, 12) });

        var summary = new BnplSummary(plans);
        summary.ActivePlans.Should().Be(2);
        summary.TotalRemainingGr.Should().Be(12_450 + 166_500);

        RecurringDetector.Detect(transactions).Groups.Should().OnlyContain(g => g.Type == RecurringType.Bnpl).And.HaveCount(2);
    }

    [Fact]
    public void Completed_bnpl_plan_is_inactive_without_next_date()
    {
        var transactions = new[] { 1, 2, 3 }
            .Select(n => Tx($"2026-0{6 + n}-07", -9_999, "Modivo", Category.Bnpl, $"PAYPO*MODIVO RATA {n}/3", isBnpl: true))
            .ToList();

        var plan = BnplPlanBuilder.Build(transactions).Single();

        plan.Active.Should().BeFalse();
        plan.NextDate.Should().BeNull();
        plan.RemainingGr.Should().Be(0);
    }
}

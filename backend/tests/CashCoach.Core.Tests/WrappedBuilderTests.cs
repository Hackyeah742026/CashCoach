using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class WrappedBuilderTests
{
    [Fact]
    public void Monthly_stats_are_computed_from_the_month_and_compared_with_the_previous_one()
    {
        var builder = new SnapshotBuilder();
        builder.Spend("2026-08-05", 100m, "Lidl");
        builder.Spend("2026-08-06", 100m, "Glovo", Category.FoodDelivery);
        builder.Spend("2026-09-01", 1000m, "Czynsz", Category.Rent);
        builder.Spend("2026-09-02", 40m, "Glovo", Category.FoodDelivery);
        builder.Spend("2026-09-09", 40m, "Glovo", Category.FoodDelivery);
        builder.Spend("2026-09-12", 110m, "Lidl");
        builder.Expense("2026-09-06", 23.99m, "Spotify", Category.Subscriptions, recurring: true);
        builder.Income("2026-09-10", 3000m);

        var stats = WrappedBuilder.Build(builder.Build().Transactions, new DateOnly(2026, 9, 1), 15_000);

        stats.TotalSpentGr.Should().Be(121_399);
        stats.TotalIncomeGr.Should().Be(300_000);
        stats.TransactionCount.Should().Be(6);
        stats.ChangePct.Should().Be(507);
        stats.TopCategories[0].Category.Should().Be(Category.Rent);
        stats.TopMerchant!.Name.Should().Be("Glovo");
        stats.TopMerchant.Count.Should().Be(2);
        stats.Delivery.AmountGr.Should().Be(8_000);
        stats.Fun.Should().Be(new FunEquivalent("pizza", 2, WrappedBuilder.PizzaPriceGr, 8_000));
        stats.BiggestDay!.Date.Should().Be(new DateOnly(2026, 9, 1));
        stats.Subscriptions.Count.Should().Be(1);
        stats.BiggestChange!.Category.Should().Be(Category.FoodDelivery);
        stats.BiggestChange.ChangePct.Should().Be(-20);
        stats.PotentialSavingsGr.Should().Be(15_000);
    }

    [Fact]
    public void Every_card_type_has_a_title_and_caption_in_both_languages()
    {
        var builder = new SnapshotBuilder();
        builder.Spend("2026-09-02", 40m, "Glovo", Category.FoodDelivery);
        var stats = WrappedBuilder.Build(builder.Build().Transactions, new DateOnly(2026, 9, 1), 0);

        WrappedCardTypes.All.Should().HaveCount(8);
        foreach (var type in WrappedCardTypes.All)
        {
            foreach (var language in new[] { "pl", "en" })
            {
                var (title, caption) = CoachTexts.WrappedCardText(type, stats, language);
                title.Should().NotBeNullOrWhiteSpace();
                caption.Should().NotBeNullOrWhiteSpace();
            }
        }
    }

    [Theory]
    [InlineData(1, "zamówienie")]
    [InlineData(3, "zamówienia")]
    [InlineData(5, "zamówień")]
    [InlineData(12, "zamówień")]
    [InlineData(22, "zamówienia")]
    public void Polish_plurals(int count, string expected) =>
        CoachTexts.PolishPlural(count, "zamówienie", "zamówienia", "zamówień").Should().Be(expected);
}

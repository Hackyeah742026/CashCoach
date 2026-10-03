using CashCoach.Core.Abstractions;
using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Categorization;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CashCoach.Api.Tests;

public class DictionaryLoadTests
{
    [Fact]
    public async Task Embedded_dictionary_categorizes_known_merchants()
    {
        var loaded = JsonMerchantDictionary.LoadEmbedded();
        loaded.Should().HaveCountGreaterThan(100);
        loaded.Should().Contain(entry => entry.Key == "ZABKA" && entry.Category == Category.Groceries);

        var dictionary = new JsonMerchantDictionary();
        var categorizer = new Categorizer(dictionary, new NoOpLlmCategorizer());
        var czynsz = MerchantNormalizer.Normalize("PRZELEW CZYNSZ POKOJ").MerchantKey;
        var matches = await categorizer.CategorizeAsync([czynsz, "ZABKA"], CancellationToken.None);

        matches["ZABKA"].Category.Should().Be(Category.Groceries);
        matches[czynsz].Should().Be(new MerchantMatch("Czynsz", Category.Rent, CategorySource.Dictionary));

        await using var factory = new ApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var resolved = scope.ServiceProvider.GetRequiredService<IMerchantDictionary>();
        resolved.Find("ZABKA").Should().NotBeNull();
        resolved.Entries.Should().HaveCountGreaterThan(100);
    }
}

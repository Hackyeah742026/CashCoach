using CashCoach.Core.Abstractions;
using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using FluentAssertions;

namespace CashCoach.Core.Tests;

public class CategorizerTests
{
    private readonly FakeDictionary _dictionary = new(
        new MerchantEntry("ZABKA", "Żabka", Category.Groceries),
        new MerchantEntry("ROSSMANN", "Rossmann", Category.Health),
        new MerchantEntry("UBER", "Uber", Category.Transport),
        new MerchantEntry("UBER EATS", "Uber Eats", Category.FoodDelivery));

    [Fact]
    public async Task Exact_match_uses_the_dictionary_without_calling_the_llm()
    {
        var llm = new FakeLlm();
        var categorizer = new Categorizer(_dictionary, llm);

        var matches = await categorizer.CategorizeAsync(["ZABKA", "UBER EATS"], CancellationToken.None);

        matches["ZABKA"].Should().Be(new MerchantMatch("Żabka", Category.Groceries, CategorySource.Dictionary));
        matches["UBER EATS"].Category.Should().Be(Category.FoodDelivery);
        llm.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Exact_match_falls_back_to_the_first_token()
    {
        var matches = await new Categorizer(_dictionary, new FakeLlm()).CategorizeAsync(["UBER TRIP"], CancellationToken.None);

        matches["UBER TRIP"].Should().Be(new MerchantMatch("Uber", Category.Transport, CategorySource.Dictionary));
    }

    [Fact]
    public async Task Misspelled_merchant_matches_fuzzily_at_85_or_above()
    {
        var llm = new FakeLlm();

        var matches = await new Categorizer(_dictionary, llm).CategorizeAsync(["ROSMANN"], CancellationToken.None);

        matches["ROSMANN"].Should().Be(new MerchantMatch("Rossmann", Category.Health, CategorySource.Fuzzy));
        llm.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_merchant_goes_to_the_llm_and_becomes_other_when_it_has_no_answer()
    {
        var llm = new FakeLlm();

        var matches = await new Categorizer(_dictionary, llm).CategorizeAsync(["PHU POLMAX", "ZABKA"], CancellationToken.None);

        matches["PHU POLMAX"].Should().Be(new MerchantMatch("Phu Polmax", Category.Other, CategorySource.Other));
        llm.Calls.Should().ContainSingle().Which.Should().Equal("PHU POLMAX");
    }

    [Fact]
    public async Task Llm_answers_are_learned_so_the_merchant_is_not_asked_twice()
    {
        var llm = new FakeLlm(new Dictionary<string, Category> { ["KWIACIARNIA ROZA"] = Category.Shopping });
        var categorizer = new Categorizer(_dictionary, llm);

        var first = await categorizer.CategorizeAsync(["KWIACIARNIA ROZA"], CancellationToken.None);
        var second = await categorizer.CategorizeAsync(["KWIACIARNIA ROZA"], CancellationToken.None);

        first["KWIACIARNIA ROZA"].Should().Be(new MerchantMatch("Kwiaciarnia Roza", Category.Shopping, CategorySource.Llm));
        second["KWIACIARNIA ROZA"].Source.Should().Be(CategorySource.Dictionary);
        llm.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Llm_is_asked_in_batches_of_at_most_50_keys()
    {
        var llm = new FakeLlm();
        var keys = Enumerable.Range(0, 120).Select(i => $"QQ{new string((char)('A' + i % 26), 3)}X{i:D3}").ToList();

        await new Categorizer(new FakeDictionary(), llm).CategorizeAsync(keys, CancellationToken.None);

        llm.Calls.Select(batch => batch.Count).Should().Equal(50, 50, 20);
    }

    private sealed class FakeDictionary(params MerchantEntry[] entries) : IMerchantDictionary
    {
        private readonly Dictionary<string, MerchantEntry> _entries = entries.ToDictionary(entry => entry.Key);

        public IReadOnlyCollection<MerchantEntry> Entries => _entries.Values;

        public MerchantEntry? Find(string key) => _entries.GetValueOrDefault(key);

        public void Learn(MerchantEntry entry) => _entries.TryAdd(entry.Key, entry);
    }

    private sealed class FakeLlm(Dictionary<string, Category>? answers = null) : ILlmCategorizer
    {
        public List<IReadOnlyList<string>> Calls { get; } = [];

        public Task<IReadOnlyDictionary<string, Category>> CategorizeAsync(IReadOnlyList<string> merchantKeys, CancellationToken cancellationToken)
        {
            Calls.Add(merchantKeys);
            return Task.FromResult<IReadOnlyDictionary<string, Category>>(answers ?? []);
        }
    }
}

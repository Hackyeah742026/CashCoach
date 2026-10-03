using CashCoach.Core.Abstractions;
using CashCoach.Core.Domain;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Categorization;
using CashCoach.Infrastructure.SyntheticData;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace CashCoach.Api.Tests;

public class SyntheticPipelineTests
{
    public static TheoryData<Persona> Personas()
    {
        var data = new TheoryData<Persona>();
        foreach (var persona in Enum.GetValues<Persona>())
        {
            data.Add(persona);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Personas))]
    public async Task Synthetic_history_is_categorized_and_planted_recurring_payments_are_found(Persona persona)
    {
        var profile = SyntheticPersonas.For(persona);
        var rows = SyntheticDataGenerator.Generate(persona);
        rows.Should().HaveCountGreaterThanOrEqualTo(150).And.HaveCountLessThanOrEqualTo(250);

        var dictionary = new JsonMerchantDictionary();
        var knownKeys = dictionary.Entries.Select(entry => entry.Key).ToHashSet(StringComparer.Ordinal);
        var llm = new RecordingLlm();
        var matches = await new Categorizer(dictionary, llm).CategorizeAsync(
            rows.Select(row => MerchantNormalizer.Normalize(row.Description).MerchantKey), CancellationToken.None);

        var transactions = rows.Select(row =>
        {
            var normalized = MerchantNormalizer.Normalize(row.Description);
            var match = matches[normalized.MerchantKey];
            return new Transaction
            {
                Id = Guid.NewGuid(),
                Date = row.Date,
                AmountGr = row.AmountGr,
                RawDescription = row.Description,
                Merchant = match.Name,
                Category = normalized.InstalmentCount is not null ? Category.Bnpl : match.Category,
                Channel = normalized.Channel,
                IsBnpl = normalized.IsBnpl,
            };
        }).ToList();

        transactions.Count(transaction => transaction.Category != Category.Other)
            .Should().BeGreaterThanOrEqualTo((int)Math.Ceiling(transactions.Count * 0.95));
        llm.Keys.Should().OnlyContain(key => !knownKeys.Contains(key));

        var detected = RecurringDetector.Detect(transactions);
        detected.SalaryDay.Should().Be(profile.Salary.Day);
        detected.Groups.Where(group => group.Type == RecurringType.Subscription && group.Active)
            .Select(group => group.Merchant)
            .Should().BeEquivalentTo(profile.Subscriptions.Select(entry => entry.Merchant));
        detected.Groups.Should().ContainSingle(group => group.Type == RecurringType.Rent)
            .Which.Merchant.Should().Be(profile.Rent.Merchant);

        var overview = SubscriptionOverviewCalculator.Build(detected.Groups
            .Where(group => group.Type == RecurringType.Subscription && group.Active)
            .Select(group => new RecurringGroup
            {
                Merchant = group.Merchant,
                Type = RecurringType.Subscription,
                AvgAmountGr = group.AvgAmountGr,
                PeriodDays = group.PeriodDays,
                Active = true,
            }));
        overview.Items.Where(item => item.Duplicate).Select(item => item.Subscription.Merchant)
            .Should().BeEquivalentTo("Spotify", "YouTube Music");

        var plans = BnplPlanBuilder.Build(transactions);
        foreach (var seed in profile.BnplPlans)
        {
            var plan = plans.Should().ContainSingle(item => item.Provider == seed.Provider && item.Merchant == seed.Merchant).Subject;
            plan.Total.Should().Be(seed.Total);
            plan.Paid.Should().Be(PaidInstalments(seed));
        }
    }

    [Fact]
    public void Checked_in_sample_csvs_match_the_generator()
    {
        var directory = SampleCsvWriter.FindSamplesDirectory(AppContext.BaseDirectory);
        foreach (var persona in Enum.GetValues<Persona>())
        {
            var path = Path.Combine(directory, SampleCsvWriter.FileName(persona));
            File.ReadAllText(path).Should().Be(SyntheticDataGenerator.GenerateCsv(persona));
        }
    }

    [Fact]
    public async Task The_app_uses_the_embedded_merchant_dictionary()
    {
        await using var factory = new ApiFactory();
        await using var scope = factory.Services.CreateAsyncScope();

        var dictionary = scope.ServiceProvider.GetRequiredService<IMerchantDictionary>();

        dictionary.Entries.Should().HaveCountGreaterThan(100);
        dictionary.Find("ZABKA")!.Category.Should().Be(Category.Groceries);
    }

    private static int PaidInstalments(BnplPlanSeed plan)
    {
        var paid = 0;
        for (int number = plan.FirstNumber, offset = 0; number <= plan.Total; number++, offset++)
        {
            if (plan.FirstDate.AddMonths(offset) > SyntheticDataGenerator.PeriodEnd)
            {
                break;
            }

            paid = number;
        }

        return paid;
    }

    private sealed class RecordingLlm : ILlmCategorizer
    {
        public List<string> Keys { get; } = [];

        public Task<IReadOnlyDictionary<string, Category>> CategorizeAsync(
            IReadOnlyList<string> merchantKeys, CancellationToken cancellationToken)
        {
            Keys.AddRange(merchantKeys);
            return Task.FromResult<IReadOnlyDictionary<string, Category>>(new Dictionary<string, Category>());
        }
    }
}

using System.Globalization;
using CashCoach.Core.Abstractions;
using CashCoach.Core.Domain;
using FuzzySharp;

namespace CashCoach.Core.Services;

public enum CategorySource
{
    Dictionary,
    Fuzzy,
    Llm,
    Other,
}

public sealed record MerchantMatch(string Name, Category Category, CategorySource Source);

/// <summary>Dictionary, then fuzzy match, then a batched LLM fallback; anything left is <see cref="Category.Other"/>.</summary>
public sealed class Categorizer(IMerchantDictionary dictionary, ILlmCategorizer llm)
{
    public const int FuzzyThreshold = 85;
    public const int LlmBatchSize = 50;

    public async Task<IReadOnlyDictionary<string, MerchantMatch>> CategorizeAsync(
        IEnumerable<string> merchantKeys, CancellationToken cancellationToken)
    {
        var matches = new Dictionary<string, MerchantMatch>();
        var unresolved = new List<string>();

        foreach (var key in merchantKeys.Distinct())
        {
            if (MatchExact(key) is { } exact)
            {
                matches[key] = new MerchantMatch(exact.Name, exact.Category, CategorySource.Dictionary);
            }
            else if (MatchFuzzy(key) is { } fuzzy)
            {
                matches[key] = new MerchantMatch(fuzzy.Name, fuzzy.Category, CategorySource.Fuzzy);
            }
            else if (key == MerchantNormalizer.UnknownMerchantKey)
            {
                matches[key] = new MerchantMatch(DisplayName(key), Category.Other, CategorySource.Other);
            }
            else
            {
                unresolved.Add(key);
            }
        }

        foreach (var batch in unresolved.Chunk(LlmBatchSize))
        {
            var answers = await llm.CategorizeAsync(batch, cancellationToken);
            foreach (var key in batch)
            {
                if (answers.TryGetValue(key, out var category) && category != Category.Other)
                {
                    var learned = new MerchantEntry(key, DisplayName(key), category);
                    dictionary.Learn(learned);
                    matches[key] = new MerchantMatch(learned.Name, category, CategorySource.Llm);
                }
                else
                {
                    matches[key] = new MerchantMatch(DisplayName(key), Category.Other, CategorySource.Other);
                }
            }
        }

        return matches;
    }

    /// <summary>The full key first, then its first token, so <c>LIDL STORE</c> still finds <c>LIDL</c>.</summary>
    private MerchantEntry? MatchExact(string key)
    {
        if (dictionary.Find(key) is { } entry)
        {
            return entry;
        }

        var firstToken = key.Split(' ')[0];
        return firstToken != key ? dictionary.Find(firstToken) : null;
    }

    private MerchantEntry? MatchFuzzy(string key) => dictionary.Entries
        .Select(entry => (Entry: entry, Score: Fuzz.TokenSetRatio(key, entry.Key)))
        .Where(candidate => candidate.Score >= FuzzyThreshold)
        .OrderByDescending(candidate => candidate.Score)
        .ThenByDescending(candidate => Fuzz.Ratio(key, candidate.Entry.Key))
        .Select(candidate => candidate.Entry)
        .FirstOrDefault();

    private static string DisplayName(string key) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.ToLowerInvariant());
}

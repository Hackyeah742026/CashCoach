using CashCoach.Core.Domain;

namespace CashCoach.Core.Abstractions;

/// <param name="Key">A normalized merchant key, as produced by <c>MerchantNormalizer</c>.</param>
/// <param name="Name">Display name, e.g. <c>Żabka</c>.</param>
public sealed record MerchantEntry(string Key, string Name, Category Category);

/// <summary>Known merchants by normalized key. Shared by all users; learned entries are kept for the process lifetime.</summary>
public interface IMerchantDictionary
{
    IReadOnlyCollection<MerchantEntry> Entries { get; }

    MerchantEntry? Find(string key);

    /// <summary>Adds a merchant categorized outside the dictionary so it is not looked up again. Existing keys are kept.</summary>
    void Learn(MerchantEntry entry);
}

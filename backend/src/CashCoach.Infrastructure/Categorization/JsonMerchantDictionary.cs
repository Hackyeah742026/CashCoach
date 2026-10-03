using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using CashCoach.Core.Abstractions;

namespace CashCoach.Infrastructure.Categorization;

/// <summary>The merchant dictionary from the embedded <c>merchants.json</c>, plus merchants learned at runtime (in memory only).</summary>
public sealed class JsonMerchantDictionary : IMerchantDictionary
{
    private const string ResourceName = "CashCoach.Infrastructure.Categorization.merchants.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
    };

    private readonly ConcurrentDictionary<string, MerchantEntry> _entries;
    private readonly Lock _snapshotLock = new();
    private MerchantEntry[] _snapshot;

    public JsonMerchantDictionary() : this(LoadEmbedded())
    {
    }

    // Internal so the DI container cannot select this greediest constructor and inject an empty list.
    internal JsonMerchantDictionary(IEnumerable<MerchantEntry> entries)
    {
        // ToDictionary throws on a duplicate key, so a broken merchants.json fails at startup.
        _entries = new ConcurrentDictionary<string, MerchantEntry>(entries.ToDictionary(entry => entry.Key));
        _snapshot = _entries.Values.ToArray();
    }

    public IReadOnlyCollection<MerchantEntry> Entries => Volatile.Read(ref _snapshot);

    public MerchantEntry? Find(string key) => _entries.GetValueOrDefault(key);

    public void Learn(MerchantEntry entry)
    {
        if (!_entries.TryAdd(entry.Key, entry))
        {
            return;
        }

        lock (_snapshotLock)
        {
            Volatile.Write(ref _snapshot, _entries.Values.ToArray());
        }
    }

    public static IReadOnlyList<MerchantEntry> LoadEmbedded()
    {
        using var stream = typeof(JsonMerchantDictionary).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        return JsonSerializer.Deserialize<List<MerchantEntry>>(stream, JsonOptions)
            ?? throw new InvalidOperationException("merchants.json is empty.");
    }
}

using System.Text.Json;
using CashCoach.Core.Abstractions;
using CashCoach.Core.Ai;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CashCoach.Infrastructure.Ai;

/// <summary>
/// Asks the model to rephrase template texts. Every rewritten text is fact-checked against the given facts and its own template;
/// texts that fail (or a failed call) are simply left out, so callers keep the template. Results are cached.
/// </summary>
public sealed class AiCopywriter(ILlmClient llm, IMemoryCache cache, ILogger<AiCopywriter> logger)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
    public static readonly TimeSpan FailureCacheFor = TimeSpan.FromMinutes(2);

    /// <param name="cacheKey">Identifies the user, language and the facts version (e.g. month).</param>
    /// <param name="facts">Computed numbers (zł) the texts may use.</param>
    /// <returns>Verified rewrites by text id.</returns>
    public async Task<IReadOnlyDictionary<string, string>> RewriteAsync(
        string cacheKey, string task, IReadOnlyDictionary<string, string> templates, object facts, string language, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var factsJson = JsonSerializer.Serialize(facts, Tools.ToolRegistry.Json);
        var system = PromptLibrary.Load(PromptLibrary.Captions, new Dictionary<string, string>
        {
            ["task"] = task,
            ["lang_name"] = PromptLibrary.LanguageName(language),
        });
        var input = JsonSerializer.Serialize(new { facts = JsonDocument.Parse(factsJson).RootElement, texts = templates });

        var verified = new Dictionary<string, string>();
        try
        {
            var response = await llm.CompleteAsync(new LlmRequest(system, [new LlmMessage(LlmRole.User, input)], JsonMode: true), cancellationToken);
            var rewritten = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(StripFences(response.Text ?? "{}")) ?? [];
            var allowed = NumberValidator.Flatten(factsJson);
            foreach (var (id, template) in templates)
            {
                if (!rewritten.TryGetValue(id, out var value) || value.ValueKind != JsonValueKind.String || value.GetString() is not { Length: > 0 } text)
                {
                    continue;
                }

                var check = NumberValidator.Validate(text, allowed.Concat(NumberValidator.Extract(template).SelectMany(n => n.Candidates)));
                if (check.Passed)
                {
                    verified[id] = text.Trim();
                }
                else
                {
                    logger.LogWarning("Copy fact check failed for {Id}: {Numbers}", id, string.Join(", ", check.Unverified));
                }
            }
        }
        catch (Exception exception) when (exception is LlmUnavailableException or JsonException)
        {
            logger.LogInformation("AI copy unavailable, using templates: {Reason}", exception.Message);
            cache.Set(cacheKey, (IReadOnlyDictionary<string, string>)verified, FailureCacheFor);
            return verified;
        }

        cache.Set(cacheKey, (IReadOnlyDictionary<string, string>)verified, CacheFor);
        return verified;
    }

    private static string StripFences(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('\n');
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? trimmed[(start + 1)..end] : trimmed;
    }
}

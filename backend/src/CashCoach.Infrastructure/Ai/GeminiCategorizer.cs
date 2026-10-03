using System.Text.Json;
using CashCoach.Core.Abstractions;
using CashCoach.Core.Ai;
using CashCoach.Core.Domain;
using Microsoft.Extensions.Logging;

namespace CashCoach.Infrastructure.Ai;

/// <summary>LLM fallback for merchant keys the dictionary and fuzzy match could not resolve. Sends only the keys; any failure leaves them as <c>other</c>.</summary>
public sealed class GeminiCategorizer(ILlmClient llm, ILogger<GeminiCategorizer> logger) : ILlmCategorizer
{
    public async Task<IReadOnlyDictionary<string, Category>> CategorizeAsync(IReadOnlyList<string> merchantKeys, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Category>();
        var keys = merchantKeys.Where(key => Anonymizer.Scrub(key) == key).ToList();
        if (keys.Count == 0)
        {
            return result;
        }

        var system = PromptLibrary.Load(PromptLibrary.Categorize, new Dictionary<string, string>
        {
            ["categories"] = string.Join(", ", SnakeCaseEnum<Category>.AllNames.Where(name => name is not ("salary" or "transfers"))),
        });

        try
        {
            var response = await llm.CompleteAsync(new LlmRequest(system, [new LlmMessage(LlmRole.User, JsonSerializer.Serialize(keys))], JsonMode: true), cancellationToken);
            var answers = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Text ?? "{}") ?? [];
            foreach (var key in keys)
            {
                if (answers.TryGetValue(key, out var name) && SnakeCaseEnum<Category>.TryParse(name, out var category))
                {
                    result[key] = category;
                }
            }

            logger.LogInformation("LLM categorized {Count} of {Total} merchant keys.", result.Count, keys.Count);
        }
        catch (Exception exception) when (exception is LlmUnavailableException or JsonException)
        {
            logger.LogInformation("LLM categorization skipped: {Reason}", exception.Message);
        }

        return result;
    }
}

using System.Text.Json;
using CashCoach.Core.Abstractions;
using CashCoach.Core.Ai;
using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Ai.Tools;
using CashCoach.Infrastructure.Analytics;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CashCoach.Infrastructure.Ai;

public static class FactCheck
{
    public const string Passed = "passed";
    public const string Fallback = "fallback";
}

/// <param name="Role"><c>user</c> or <c>assistant</c>.</param>
public sealed record ChatHistoryMessage(string Role, string Content);

/// <param name="Fallback">The answer is a template built from computed facts, not AI text.</param>
/// <param name="FactCheck"><c>passed</c> (every number verified) or <c>fallback</c>.</param>
public sealed record ChatTurn(
    Guid ConversationId,
    Guid MessageId,
    string Answer,
    IReadOnlyList<string> ToolsUsed,
    IReadOnlyList<Guid> TransactionIds,
    IReadOnlyList<ToolFigure> Figures,
    bool Fallback,
    string FactCheck,
    DateTime CreatedAt);

/// <summary>Stored in <see cref="ChatMessage.FactsJson"/> for assistant messages.</summary>
public sealed record StoredFacts(IReadOnlyList<string> ToolsUsed, IReadOnlyList<Guid> TransactionIds, IReadOnlyList<ToolFigure> Figures, bool Fallback, string FactCheck);

/// <summary>Gemini with function calling. Every number in the answer must come from a tool result, or the answer is retried and finally replaced by a template.</summary>
public sealed class ChatAgent(
    ILlmClient llm, ToolRegistry tools, AppDbContext db, SnapshotLoader loader, TimeProvider timeProvider, ILogger<ChatAgent> logger)
{
    public const int HistoryLimit = 10;
    public const int MaxToolRounds = 5;
    public const int MaxRetries = 2;
    public const int MaxOutputTokens = 400;

    public async Task<ChatTurn> RunChatAsync(
        Guid userId,
        Guid? conversationId,
        string message,
        string language,
        IReadOnlyList<ChatHistoryMessage>? clientHistory,
        Func<string, Task>? onTool,
        CancellationToken cancellationToken)
    {
        var conversation = conversationId ?? Guid.NewGuid();
        var question = Anonymizer.Scrub(message.Trim());
        var history = await HistoryAsync(userId, conversation, clientHistory, cancellationToken);
        var context = new ToolContext(userId, language);
        var snapshot = context.Snapshot = await loader.LoadAsync(userId, cancellationToken);

        var system = PromptLibrary.Load(PromptLibrary.ChatSystem, new Dictionary<string, string>
        {
            ["today"] = $"{snapshot.AsOf:yyyy-MM-dd}",
            ["lang_name"] = PromptLibrary.LanguageName(language),
        });

        var messages = history.Append(new LlmMessage(LlmRole.User, question)).ToList();
        var facts = new List<decimal>(NumberValidator.Extract(question).SelectMany(n => n.Candidates));
        var toolsUsed = new List<string>();
        var transactionIds = new List<Guid>();
        var figures = new List<ToolFigure>();

        string? answer = null;
        try
        {
            for (var attempt = 0; attempt <= MaxRetries && answer is null; attempt++)
            {
                var text = await RunToolLoopAsync(system, messages, context, facts, toolsUsed, transactionIds, figures, onTool, cancellationToken);
                var check = NumberValidator.Validate(text, facts);
                if (check.Passed)
                {
                    answer = text;
                    break;
                }

                logger.LogWarning("Chat fact check failed (attempt {Attempt}): unverified numbers {Numbers}", attempt + 1, string.Join(", ", check.Unverified));
                messages.Add(new LlmMessage(LlmRole.Model, text));
                messages.Add(new LlmMessage(LlmRole.User,
                    $"Fact check failed: these numbers are not in any tool result: {string.Join(", ", check.Unverified)}. " +
                    "Answer the previous question again using only numbers copied from tool results (call a tool if you need one). Do not mention this check."));
            }
        }
        catch (LlmUnavailableException exception)
        {
            logger.LogWarning("Chat model unavailable: {Reason}", exception.Message);
        }

        var fallback = answer is null;
        if (fallback)
        {
            (answer, var fallbackFigures, var fallbackIds) = Fallback(snapshot, language);
            figures.AddRange(fallbackFigures);
            transactionIds.AddRange(fallbackIds);
        }

        return await SaveAsync(userId, conversation, question, answer!, new StoredFacts(
            toolsUsed.Distinct().ToList(),
            transactionIds.Distinct().Take(100).ToList(),
            figures.DistinctBy(f => f.Key).ToList(),
            fallback,
            fallback ? FactCheck.Fallback : FactCheck.Passed), cancellationToken);
    }

    private async Task<string> RunToolLoopAsync(
        string system, List<LlmMessage> messages, ToolContext context, List<decimal> facts,
        List<string> toolsUsed, List<Guid> transactionIds, List<ToolFigure> figures,
        Func<string, Task>? onTool, CancellationToken cancellationToken)
    {
        for (var round = 0; round <= MaxToolRounds; round++)
        {
            // After the last allowed round the model must answer from what it has.
            var offerTools = round < MaxToolRounds;
            var response = await llm.CompleteAsync(
                new LlmRequest(system, messages.ToList(), offerTools ? ToolRegistry.Definitions : null, MaxOutputTokens: MaxOutputTokens), cancellationToken);

            if (response.ToolCalls.Count == 0 || !offerTools)
            {
                return response.Text ?? throw new LlmUnavailableException("The model returned no text.");
            }

            var results = new List<LlmToolResult>();
            foreach (var call in response.ToolCalls)
            {
                if (onTool is not null)
                {
                    await onTool(call.Name);
                }

                var output = await tools.ExecuteAsync(call.Name, call.ArgumentsJson, context, cancellationToken);
                var json = JsonSerializer.Serialize(output.Result, ToolRegistry.Json);
                facts.AddRange(NumberValidator.Flatten(json));
                toolsUsed.Add(call.Name);
                transactionIds.AddRange(output.TransactionIds);
                figures.AddRange(output.Figures);
                results.Add(new LlmToolResult(call.Id, call.Name, json));
            }

            messages.Add(new LlmMessage(LlmRole.Model, response.Text, response.ToolCalls, ProviderContent: response.ProviderContent));
            messages.Add(new LlmMessage(LlmRole.User, ToolResults: results));
        }

        throw new LlmUnavailableException("Too many tool rounds.");
    }

    /// <summary>A template answer with the key computed numbers, used when the model is unavailable or keeps failing the fact check.</summary>
    public static (string Text, IReadOnlyList<ToolFigure> Figures, IReadOnlyList<Guid> TransactionIds) Fallback(FinancialSnapshot snapshot, string language)
    {
        var f = ForecastCalculator.Compute(snapshot);
        var en = CoachTexts.IsEnglish(language);
        string Z(long gr) => CoachTexts.Zl(gr, language);
        var payday = CoachTexts.Date(f.NextPayday, language);
        var status = f.Status switch
        {
            ForecastStatus.Danger => en
                ? $" At this pace the money runs out on {CoachTexts.Date(f.RunOutDate ?? f.NextPayday, language)}."
                : $" W tym tempie pieniądze skończą się {CoachTexts.Date(f.RunOutDate ?? f.NextPayday, language)}.",
            ForecastStatus.Tight => en ? " It will be tight until payday." : " Do wypłaty będzie krucho.",
            _ => en ? " You're on track until payday." : " Do wypłaty powinno wystarczyć.",
        };
        var text = en
            ? $"I can't prepare an AI answer right now, so here are your key numbers: balance {Z(f.BalanceGr)}, safe to spend until payday ({payday}) {Z(f.SafeToSpendGr)}.{status}"
            : $"Nie mogę teraz przygotować odpowiedzi AI, więc podaję najważniejsze liczby: saldo {Z(f.BalanceGr)}, bezpiecznie do wydania do wypłaty ({payday}) {Z(f.SafeToSpendGr)}.{status}";
        IReadOnlyList<ToolFigure> figures =
        [
            new("forecast.balance", en ? "Current balance" : "Saldo konta", f.BalanceGr),
            new("forecast.fixed_upcoming", en ? "Bills before payday" : "Rachunki przed wypłatą", -f.FixedUpcomingGr),
            new("forecast.safety_buffer", en ? "Safety buffer" : "Poduszka bezpieczeństwa", -f.SafetyBufferGr),
            new("forecast.safe_to_spend", en ? "Safe to spend" : "Bezpiecznie do wydania", f.SafeToSpendGr),
        ];
        return (text, figures, f.UpcomingTransactionIds);
    }

    private async Task<List<LlmMessage>> HistoryAsync(
        Guid userId, Guid conversationId, IReadOnlyList<ChatHistoryMessage>? clientHistory, CancellationToken cancellationToken)
    {
        var stored = await db.ChatMessages.AsNoTracking()
            .Where(m => m.UserId == userId && m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);

        var history = stored.Count > 0
            ? stored.OrderBy(m => m.CreatedAt).Select(m => new ChatHistoryMessage(m.Role, m.Content)).ToList()
            : (clientHistory ?? []).TakeLast(HistoryLimit).ToList();

        return history
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new LlmMessage(m.Role == "assistant" ? LlmRole.Model : LlmRole.User, Anonymizer.Scrub(m.Content)))
            .ToList();
    }

    private async Task<ChatTurn> SaveAsync(Guid userId, Guid conversationId, string question, string answer, StoredFacts facts, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var reply = new ChatMessage
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConversationId = conversationId,
            Role = "assistant",
            Content = answer,
            FactsJson = JsonSerializer.Serialize(facts, ToolRegistry.Json),
            CreatedAt = now.AddTicks(1),
        };
        db.ChatMessages.Add(new ChatMessage { Id = Guid.NewGuid(), UserId = userId, ConversationId = conversationId, Role = "user", Content = question, CreatedAt = now });
        db.ChatMessages.Add(reply);
        await db.SaveChangesAsync(cancellationToken);

        return new ChatTurn(conversationId, reply.Id, answer, facts.ToolsUsed, facts.TransactionIds, facts.Figures, facts.Fallback, facts.FactCheck, reply.CreatedAt);
    }

    public async Task<IReadOnlyList<(ChatMessage Message, StoredFacts? Facts)>> ConversationAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var messages = await db.ChatMessages.AsNoTracking()
            .Where(m => m.UserId == userId && m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);
        return messages
            .Select(m => (m, m.FactsJson is null ? null : JsonSerializer.Deserialize<StoredFacts>(m.FactsJson, ToolRegistry.Json)))
            .ToList();
    }

    /// <summary>Personalized starter questions built from the user's own data (no model call).</summary>
    public static IReadOnlyList<string> Suggestions(FinancialSnapshot snapshot, string language)
    {
        var en = CoachTexts.IsEnglish(language);
        var topMerchant = snapshot.ExpensesBetween(snapshot.AsOf.AddDays(-29), snapshot.AsOf)
            .Where(t => t.Category is Category.FoodDelivery or Category.Restaurants or Category.Transport or Category.Shopping)
            .GroupBy(t => t.Merchant)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        var month = CoachTexts.MonthName(new DateOnly(snapshot.AsOf.Year, snapshot.AsOf.Month, 1), language);

        var suggestions = new List<string>
        {
            en ? "Will my money last until payday?" : "Czy wystarczy mi do wypłaty?",
            en ? "Can I afford concert tickets for 250 zł?" : "Czy stać mnie na bilet na koncert za 250 zł?",
        };
        if (topMerchant is not null)
        {
            suggestions.Add(en ? $"How much did I spend on {topMerchant} in {month}?" : $"Ile wydałem(-am) na {topMerchant} ({month})?");
        }

        suggestions.Add(en ? "Where can I save money?" : "Gdzie mogę zaoszczędzić?");
        suggestions.Add(en ? "Which subscriptions do I pay for?" : "Za jakie subskrypcje płacę?");
        return suggestions;
    }
}

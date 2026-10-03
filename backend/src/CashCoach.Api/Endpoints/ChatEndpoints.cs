using System.Text.Json;
using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Infrastructure.Ai;
using CashCoach.Infrastructure.Analytics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class ChatEndpoints
{
    public const string RateLimitPolicy = "chat";
    private const int MaxMessageLength = 1000;
    private const int DeltaChunkLength = 24;

    public static RouteGroupBuilder MapChatEndpoints(this RouteGroupBuilder api)
    {
        var chat = api.MapGroup("/chat").WithTags("Chat").RequireRateLimiting(RateLimitPolicy);

        // JSON by default; with "Accept: text/event-stream" the same turn is streamed as tool, delta, evidence and done events.
        chat.MapPost("", async (ChatRequest request, HttpContext http, CurrentUser user, ChatAgent agent, IOptions<JsonOptions> json, CancellationToken ct) =>
            {
                var message = (request.Message ?? request.Messages?.LastOrDefault(m => m.Role == "user")?.Content)?.Trim();
                if (string.IsNullOrEmpty(message) || message.Length > MaxMessageLength)
                {
                    throw BadRequest("invalid_message", $"'message' must be 1 to {MaxMessageLength} characters.");
                }

                var language = request.Language ?? user.User.Language;
                if (language is not ("pl" or "en"))
                {
                    throw BadRequest("invalid_language", "'language' must be one of: pl, en.");
                }

                // Client history without the question itself, which is passed separately.
                var history = request.Messages?
                    .Where(m => m.Role is "user" or "assistant" && !string.IsNullOrWhiteSpace(m.Content))
                    .Select(m => new ChatHistoryMessage(m.Role!, m.Content!))
                    .ToList();
                if (history is { Count: > 0 } && request.Message is null)
                {
                    history.RemoveAt(history.FindLastIndex(m => m.Role == "user"));
                }

                if (!http.Request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
                {
                    var turn = await agent.RunChatAsync(user.Id, request.ConversationId, message, language, history, null, ct);
                    return Results.Ok(ChatResponse.From(turn));
                }

                var options = json.Value.SerializerOptions;
                http.Response.ContentType = "text/event-stream";
                http.Response.Headers.CacheControl = "no-cache";
                try
                {
                    var turn = await agent.RunChatAsync(user.Id, request.ConversationId, message, language, history,
                        tool => WriteEventAsync(http, "tool", new { name = tool }, options, ct), ct);

                    // The answer is fact-checked as a whole before any of it is sent, then streamed in small pieces.
                    for (var i = 0; i < turn.Answer.Length; i += DeltaChunkLength)
                    {
                        await WriteEventAsync(http, "delta", new { text = turn.Answer.Substring(i, Math.Min(DeltaChunkLength, turn.Answer.Length - i)) }, options, ct);
                    }

                    await WriteEventAsync(http, "evidence", ChatResponse.EvidenceOf(turn.TransactionIds, turn.Figures), options, ct);
                    await WriteEventAsync(http, "done",
                        new { fact_check = turn.FactCheck, fallback = turn.Fallback, conversation_id = turn.ConversationId, message_id = turn.MessageId, tools_used = turn.ToolsUsed },
                        options, ct);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await WriteEventAsync(http, "error", new { message = "Something went wrong. Please try again." }, options, ct);
                }

                return Results.Empty;
            })
            .WithName("Chat");

        chat.MapGet("/suggestions", async (CurrentUser user, SnapshotLoader loader, CancellationToken ct) =>
                TypedResults.Ok(new ChatSuggestionsResponse(ChatAgent.Suggestions(await loader.LoadAsync(user.Id, ct), user.User.Language))))
            .WithName("ChatSuggestions");

        chat.MapGet("/{conversationId:guid}", async (Guid conversationId, CurrentUser user, ChatAgent agent, CancellationToken ct) =>
            {
                var messages = await agent.ConversationAsync(user.Id, conversationId, ct);
                if (messages.Count == 0)
                {
                    throw NotFound("conversation_not_found", "Conversation not found.");
                }

                return TypedResults.Ok(new ConversationResponse(conversationId, messages.Select(item => new ChatMessageDto(
                    item.Message.Id,
                    item.Message.Role,
                    item.Message.Content,
                    item.Message.CreatedAt,
                    item.Facts?.ToolsUsed,
                    item.Facts is { } facts ? ChatResponse.EvidenceOf(facts.TransactionIds, facts.Figures) : null,
                    item.Facts?.Fallback,
                    item.Facts?.FactCheck)).ToList()));
            })
            .WithName("GetConversation");

        return api;
    }

    private static async Task WriteEventAsync(HttpContext http, string name, object data, JsonSerializerOptions options, CancellationToken ct)
    {
        await http.Response.WriteAsync($"event: {name}\ndata: {JsonSerializer.Serialize(data, options)}\n\n", ct);
        await http.Response.Body.FlushAsync(ct);
    }
}

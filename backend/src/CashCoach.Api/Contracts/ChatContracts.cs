using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Ai;
using CashCoach.Infrastructure.Ai.Tools;

namespace CashCoach.Api.Contracts;

public sealed record ChatHistoryItem(string? Role, string? Content);

/// <param name="ConversationId">Continue a stored conversation; omit to start a new one.</param>
/// <param name="Message">The question. When omitted, the last <c>user</c> item of <paramref name="Messages"/> is used.</param>
/// <param name="Messages">Client-side history (used only when the conversation has no stored messages).</param>
/// <param name="Language"><c>pl</c> or <c>en</c>; defaults to the user's language.</param>
public sealed record ChatRequest(Guid? ConversationId, string? Message, IReadOnlyList<ChatHistoryItem>? Messages, string? Language);

/// <param name="FactCheck"><c>passed</c> when every number was verified against tool results, <c>fallback</c> for a template answer.</param>
public sealed record ChatResponse(
    Guid ConversationId,
    Guid MessageId,
    string Answer,
    IReadOnlyList<string> ToolsUsed,
    EvidenceDto Evidence,
    bool Fallback,
    string FactCheck,
    DateTime CreatedAt)
{
    public static ChatResponse From(ChatTurn turn) => new(
        turn.ConversationId, turn.MessageId, turn.Answer, turn.ToolsUsed, EvidenceOf(turn.TransactionIds, turn.Figures), turn.Fallback, turn.FactCheck, turn.CreatedAt);

    public static EvidenceDto EvidenceOf(IReadOnlyList<Guid> ids, IReadOnlyList<ToolFigure> figures) =>
        new(ids, figures.Select(f => new FigureDto(f.Key, f.Label, Money.ToZloty(f.AmountGr))).ToList());
}

public sealed record ChatMessageDto(Guid Id, string Role, string Content, DateTime CreatedAt, IReadOnlyList<string>? ToolsUsed, EvidenceDto? Evidence, bool? Fallback, string? FactCheck);

public sealed record ConversationResponse(Guid ConversationId, IReadOnlyList<ChatMessageDto> Messages);

public sealed record ChatSuggestionsResponse(IReadOnlyList<string> Items);

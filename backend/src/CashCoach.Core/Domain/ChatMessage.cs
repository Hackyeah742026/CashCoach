namespace CashCoach.Core.Domain;

public class ChatMessage
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ConversationId { get; set; }
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public string? FactsJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

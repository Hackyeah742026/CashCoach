namespace CashCoach.Core.Abstractions;

public enum LlmRole
{
    User,
    Model,
}

public sealed record LlmToolCall(string? Id, string Name, string ArgumentsJson);

public sealed record LlmToolResult(string? Id, string Name, string ResultJson);

/// <param name="ProviderContent">The provider's own message object for a model turn (e.g. to keep Gemini thought signatures); passed back unchanged.</param>
public sealed record LlmMessage(
    LlmRole Role,
    string? Text = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null,
    IReadOnlyList<LlmToolResult>? ToolResults = null,
    object? ProviderContent = null);

/// <param name="ParametersJsonSchema">JSON Schema of the arguments object.</param>
public sealed record LlmToolDefinition(string Name, string Description, string ParametersJsonSchema);

/// <param name="JsonMode">Ask for a JSON response (temperature 0).</param>
public sealed record LlmRequest(
    string System,
    IReadOnlyList<LlmMessage> Messages,
    IReadOnlyList<LlmToolDefinition>? Tools = null,
    bool JsonMode = false,
    int? MaxOutputTokens = null);

public sealed record LlmResponse(string? Text, IReadOnlyList<LlmToolCall> ToolCalls, object? ProviderContent = null);

/// <summary>The language model (Gemini in production, a fake in tests).</summary>
public interface ILlmClient
{
    /// <exception cref="LlmUnavailableException">No API key, timeout, provider error or an unusable answer.</exception>
    Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken);
}

/// <summary>The model could not produce an answer; callers fall back to templates.</summary>
public sealed class LlmUnavailableException(string message, Exception? innerException = null) : Exception(message, innerException);

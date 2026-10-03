using CashCoach.Core.Abstractions;

namespace CashCoach.Api.Tests;

/// <summary>
/// Replaces Gemini in API tests. Without a script it behaves like a missing API key;
/// with one it answers each call from the queue (a response, or an exception to throw).
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Queue<Func<LlmRequest, LlmResponse>> _script = new();

    public List<LlmRequest> Requests { get; } = [];

    /// <summary>Requests from the chat agent only (demo login also asks the categorizer).</summary>
    public List<LlmRequest> ChatRequests => Requests.Where(r => r.System.StartsWith("You are CashCoach", StringComparison.Ordinal)).ToList();

    public FakeLlmClient Then(Func<LlmRequest, LlmResponse> step)
    {
        _script.Enqueue(step);
        return this;
    }

    public FakeLlmClient ThenToolCall(string name, string argumentsJson = "{}") =>
        Then(_ => new LlmResponse(null, [new LlmToolCall($"call-{name}", name, argumentsJson)]));

    public FakeLlmClient ThenText(string text) => Then(_ => new LlmResponse(text, []));

    public Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        lock (_script)
        {
            Requests.Add(request);
            return _script.TryDequeue(out var step)
                ? Task.FromResult(step(request))
                : throw new LlmUnavailableException("Fake LLM: no scripted answer.");
        }
    }
}

public sealed class NoOpLlmCategorizer : ILlmCategorizer
{
    public Task<IReadOnlyDictionary<string, Core.Domain.Category>> CategorizeAsync(IReadOnlyList<string> merchantKeys, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, Core.Domain.Category>>(new Dictionary<string, Core.Domain.Category>());
}

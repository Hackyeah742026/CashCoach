using System.Text.Json;
using CashCoach.Core.Abstractions;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CashCoach.Infrastructure.Ai;

/// <summary><see cref="ILlmClient"/> on the official Google.GenAI SDK. The key comes from <c>GEMINI_API_KEY</c> and is never logged.</summary>
public sealed class GeminiClient : ILlmClient, IDisposable
{
    /// <summary>Fast (about 2 s per chat answer) with a usable free-tier quota; <c>gemini-3.8-flash</c> allows only 20 free requests a day.</summary>
    public const string DefaultModel = "gemini-3.5-flash-lite";
    public const float TextTemperature = 0.3f;
    public const int DefaultMaxOutputTokens = 1024;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly Client? _client;
    private readonly string _model;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(IConfiguration configuration, ILogger<GeminiClient> logger)
    {
        _logger = logger;
        _model = configuration["GEMINI_MODEL"] is { Length: > 0 } model ? model : DefaultModel;
        if (configuration["GEMINI_API_KEY"] is { Length: > 0 } key && !key.StartsWith("gemini_api_key", StringComparison.Ordinal))
        {
            _client = new Client(apiKey: key, httpOptions: new HttpOptions { Timeout = (int)Timeout.TotalMilliseconds });
        }
    }

    public bool IsConfigured => _client is not null;

    public async Task<LlmResponse> CompleteAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (_client is null)
        {
            throw new LlmUnavailableException("GEMINI_API_KEY is not set.");
        }

        var contents = request.Messages.Select(ToContent).ToList();
        var config = BuildConfig(request);

        for (var attempt = 1; ; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            try
            {
                var response = await _client.Models.GenerateContentAsync(_model, contents, config, timeout.Token);
                return ToResponse(response);
            }
            catch (Exception exception) when (attempt == 1 && IsTransient(exception, cancellationToken))
            {
                _logger.LogWarning("Gemini call failed ({Error}); retrying once.", exception.GetType().Name);
            }
            catch (LlmUnavailableException)
            {
                throw;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Provider error bodies (status, reason) never contain the key; keep them short.
                _logger.LogWarning("Gemini call failed: {Error}: {Message}", exception.GetType().Name, Truncate(exception.Message));
                throw new LlmUnavailableException("The model is unavailable.", exception);
            }
        }
    }

    private GenerateContentConfig BuildConfig(LlmRequest request)
    {
        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = request.System }] },
            Temperature = request.JsonMode ? 0f : TextTemperature,
            MaxOutputTokens = request.MaxOutputTokens ?? DefaultMaxOutputTokens,
        };

        // Thinking tokens count against the output limit and add latency; these tasks need little reasoning.
        if (_model.StartsWith("gemini-2.5-flash", StringComparison.Ordinal))
        {
            config.ThinkingConfig = new ThinkingConfig { ThinkingBudget = 0 };
        }
        else if (_model.StartsWith("gemini-3", StringComparison.Ordinal))
        {
            config.ThinkingConfig = new ThinkingConfig { ThinkingLevel = ThinkingLevel.Low };
        }

        if (request.JsonMode)
        {
            config.ResponseMimeType = "application/json";
        }

        if (request.Tools is { Count: > 0 } tools)
        {
            config.Tools =
            [
                new Tool
                {
                    FunctionDeclarations = tools.Select(tool => new FunctionDeclaration
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        ParametersJsonSchema = JsonDocument.Parse(tool.ParametersJsonSchema).RootElement.Clone(),
                    }).ToList(),
                },
            ];
        }

        return config;
    }

    private static Content ToContent(LlmMessage message)
    {
        if (message.ProviderContent is Content original)
        {
            return original;
        }

        var parts = new List<Part>();
        if (!string.IsNullOrEmpty(message.Text))
        {
            parts.Add(new Part { Text = message.Text });
        }

        foreach (var call in message.ToolCalls ?? [])
        {
            parts.Add(new Part { FunctionCall = new FunctionCall { Id = call.Id, Name = call.Name, Args = ToDictionary(call.ArgumentsJson) } });
        }

        foreach (var result in message.ToolResults ?? [])
        {
            parts.Add(new Part
            {
                FunctionResponse = new FunctionResponse
                {
                    Id = result.Id,
                    Name = result.Name,
                    Response = new Dictionary<string, object> { ["result"] = JsonDocument.Parse(result.ResultJson).RootElement.Clone() },
                },
            });
        }

        return new Content { Role = message.Role == LlmRole.Model ? "model" : "user", Parts = parts };
    }

    private static LlmResponse ToResponse(GenerateContentResponse response)
    {
        var candidate = response.Candidates?.FirstOrDefault()
            ?? throw new LlmUnavailableException("The model returned no candidates (the prompt may have been blocked).");
        // Safety blocks, recitation and malformed calls arrive without usable parts, so the empty check below covers them.
        var finish = candidate.FinishReason?.ToString();
        var parts = candidate.Content?.Parts ?? [];
        var calls = parts
            .Where(part => part.FunctionCall is not null)
            .Select(part => new LlmToolCall(part.FunctionCall!.Id, part.FunctionCall.Name ?? "", JsonSerializer.Serialize(part.FunctionCall.Args ?? new Dictionary<string, object>())))
            .ToList();
        var text = string.Concat(parts.Where(part => part.Text is not null && part.Thought != true).Select(part => part.Text));

        if (calls.Count == 0 && string.IsNullOrWhiteSpace(text))
        {
            throw new LlmUnavailableException($"The model returned an empty answer ({finish ?? "no finish reason"}).");
        }

        return new LlmResponse(string.IsNullOrWhiteSpace(text) ? null : text, calls, candidate.Content);
    }

    private static Dictionary<string, object> ToDictionary(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, object>>(string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? [];

    /// <summary>Network errors, our timeout and 5xx/429 server errors are retried once; a cancelled request is not.</summary>
    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested
        && exception is not ClientError
        && exception is HttpRequestException or TaskCanceledException or OperationCanceledException or ServerError;

    private static string Truncate(string message) => message.Length <= 300 ? message : message[..300];

    public void Dispose() => _client?.Dispose();
}

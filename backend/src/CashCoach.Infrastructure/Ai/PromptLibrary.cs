namespace CashCoach.Infrastructure.Ai;

/// <summary>Prompt templates embedded from <c>Ai/Prompts/*.md</c>; <c>{name}</c> placeholders are replaced on load.</summary>
public static class PromptLibrary
{
    public const string ChatSystem = "chat_system";
    public const string Categorize = "categorize";
    public const string Captions = "captions";

    public static string Load(string name, IReadOnlyDictionary<string, string>? values = null)
    {
        var resource = $"CashCoach.Infrastructure.Ai.Prompts.{name}.md";
        using var stream = typeof(PromptLibrary).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Prompt '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();
        foreach (var (key, value) in values ?? new Dictionary<string, string>())
        {
            text = text.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    public static string LanguageName(string language) => language == "en" ? "English" : "Polish (po polsku)";
}

using CashCoach.Core.Abstractions;
using CashCoach.Core.Domain;

namespace CashCoach.Infrastructure.Categorization;

/// <summary>Placeholder until the Gemini categorizer exists: categorizes nothing, so unresolved merchants become <c>other</c>.</summary>
public sealed class NoOpLlmCategorizer : ILlmCategorizer
{
    public Task<IReadOnlyDictionary<string, Category>> CategorizeAsync(
        IReadOnlyList<string> merchantKeys, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, Category>>(new Dictionary<string, Category>());
}

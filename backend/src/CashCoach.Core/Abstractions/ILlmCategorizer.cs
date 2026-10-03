using CashCoach.Core.Domain;

namespace CashCoach.Core.Abstractions;

/// <summary>Categorizes merchant keys the dictionary could not resolve.</summary>
public interface ILlmCategorizer
{
    /// <summary>Returns a category for each key the model is confident about; keys it omits become <see cref="Category.Other"/>.</summary>
    Task<IReadOnlyDictionary<string, Category>> CategorizeAsync(IReadOnlyList<string> merchantKeys, CancellationToken cancellationToken);
}

using CashCoach.Core.Services;
using CashCoach.Infrastructure.Import;

namespace CashCoach.Api.Contracts;

public sealed record PeriodDto(DateOnly From, DateOnly To)
{
    public static PeriodDto Of(DateRange range) => new(range.From, range.To);
}

public sealed record ImportCategorizedCounts(int Dictionary, int Fuzzy, int Llm, int Other);

public sealed record ImportRecurringFound(int Subscriptions, int Bnpl, int? SalaryDay);

public sealed record ImportResponse(
    int Imported,
    int SkippedDuplicates,
    ImportCategorizedCounts Categorized,
    ImportRecurringFound RecurringFound,
    PeriodDto? Period)
{
    public static ImportResponse From(ImportResult result) => new(
        result.Imported,
        result.SkippedDuplicates,
        new ImportCategorizedCounts(result.Categorized.Dictionary, result.Categorized.Fuzzy, result.Categorized.Llm, result.Categorized.Other),
        new ImportRecurringFound(result.Recurring.Subscriptions, result.Recurring.Bnpl, result.Recurring.SalaryDay),
        result.Period is null ? null : PeriodDto.Of(result.Period));
}

/// <param name="Persona"><c>student</c>, <c>first_job</c> or <c>bnpl_heavy</c> (default).</param>
public sealed record DemoImportRequest(string? Persona);

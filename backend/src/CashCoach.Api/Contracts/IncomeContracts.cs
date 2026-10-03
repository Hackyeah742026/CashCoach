using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;

namespace CashCoach.Api.Contracts;

/// <param name="Day">Usual day of month (31 with <c>last_working_day</c>).</param>
/// <param name="Amount">Median monthly amount in złoty; <paramref name="AmountMin"/>–<paramref name="AmountMax"/> is the range seen.</param>
public sealed record IncomeCandidateDto(
    string Source,
    IncomeKind Kind,
    int Day,
    PaydayRule DayRule,
    decimal Amount,
    decimal AmountMin,
    decimal AmountMax,
    int MonthsSeen,
    DetectionConfidence Confidence,
    EvidenceDto Evidence)
{
    public static IncomeCandidateDto From(IncomeCandidate c) => new(
        c.Source, c.Kind, c.Day, c.DayRule,
        Money.ToZloty(c.AmountGr), Money.ToZloty(c.MinGr), Money.ToZloty(c.MaxGr),
        c.MonthsSeen, c.Confidence, new EvidenceDto(c.TransactionIds, []));
}

/// <param name="Guess">The main regular income found in the data, or <c>null</c>.</param>
/// <param name="Others">Other regular incomes (e.g. pocket money next to a stipend).</param>
/// <param name="Confirmed">What the user confirmed, or <c>null</c> while unanswered.</param>
public sealed record IncomeDetectionResponse(IncomeCandidateDto? Guess, IReadOnlyList<IncomeCandidateDto> Others, IncomeDto? Confirmed);

/// <param name="HasIncome">false = no regular income (the forecast runs to the end of the month).</param>
/// <param name="Day">1 to 31; not needed with <c>last_working_day</c>.</param>
/// <param name="DayRule"><c>fixed_day</c> (default) or <c>last_working_day</c>.</param>
/// <param name="Amount">Monthly income in złoty, positive.</param>
public sealed record ConfirmIncomeRequest(bool? HasIncome, int? Day, string? DayRule, decimal? Amount, string? Source);

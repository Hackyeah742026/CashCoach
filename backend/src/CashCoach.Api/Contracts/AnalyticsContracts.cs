using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;

namespace CashCoach.Api.Contracts;

/// <param name="Key">Stable fact key, e.g. <c>forecast.safe_to_spend</c>.</param>
/// <param name="Label">Human label in the user's language.</param>
public sealed record FigureDto(string Key, string Label, decimal Amount);

/// <summary>What a number or AI claim is based on: the source transactions and the computed figures.</summary>
/// <param name="Calculation">Human-readable formula, when the number is a simple calculation.</param>
public sealed record EvidenceDto(IReadOnlyList<Guid> TransactionIds, IReadOnlyList<FigureDto> Figures, string? Calculation = null)
{
    public static readonly EvidenceDto Empty = new([], []);
}

internal static class Zl
{
    public static decimal Of(long grosze) => Money.ToZloty(grosze);

    public static decimal? Of(long? grosze) => grosze is { } value ? Money.ToZloty(value) : null;

    public static string Month(DateOnly month) => $"{month:yyyy-MM}";
}

// ---------------------------------------------------------------------------- forecast

public sealed record UpcomingPaymentDto(DateOnly Date, string Merchant, RecurringType Type, decimal Amount);

public sealed record BalancePointDto(DateOnly Date, decimal Balance);

/// <param name="Status"><c>ok</c> (≥ 200 zł left on payday), <c>tight</c> (0 to 200 zł) or <c>danger</c> (below zero).</param>
public sealed record ForecastResponse(
    DateOnly AsOf,
    DateOnly NextPayday,
    int DaysLeft,
    decimal Balance,
    decimal FixedUpcoming,
    decimal DailyVariable,
    decimal ProjectedEnd,
    DateOnly? RunOutDate,
    ForecastStatus Status,
    decimal SafetyBuffer,
    decimal SafeToSpend,
    decimal SafePerDay,
    IReadOnlyList<UpcomingPaymentDto> Upcoming,
    IReadOnlyList<BalancePointDto> Series,
    EvidenceDto Evidence)
{
    public static ForecastResponse From(Forecast f, string language) => new(
        f.AsOf,
        f.NextPayday,
        f.DaysLeft,
        Zl.Of(f.BalanceGr),
        Zl.Of(f.FixedUpcomingGr),
        Zl.Of(f.DailyVariableGr),
        Zl.Of(f.ProjectedEndGr),
        f.RunOutDate,
        f.Status,
        Zl.Of(f.SafetyBufferGr),
        Zl.Of(f.SafeToSpendGr),
        Zl.Of(f.SafePerDayGr),
        f.Upcoming.Select(p => new UpcomingPaymentDto(p.Date, p.Merchant, p.Type, Zl.Of(p.AmountGr))).ToList(),
        f.Series.Select(p => new BalancePointDto(p.Date, Zl.Of(p.BalanceGr))).ToList(),
        new EvidenceDto(f.UpcomingTransactionIds, Figures(f, language)));

    public static IReadOnlyList<FigureDto> Figures(Forecast f, string language)
    {
        var en = CoachTexts.IsEnglish(language);
        return
        [
            new("forecast.balance", en ? "Current balance" : "Saldo konta", Zl.Of(f.BalanceGr)),
            new("forecast.fixed_upcoming", en ? "Bills before payday" : "Rachunki przed wypłatą", -Zl.Of(f.FixedUpcomingGr)),
            new("forecast.safety_buffer", en ? "Safety buffer" : "Poduszka bezpieczeństwa", -Zl.Of(f.SafetyBufferGr)),
            new("forecast.safe_to_spend", en ? "Safe to spend" : "Bezpiecznie do wydania", Zl.Of(f.SafeToSpendGr)),
            new("forecast.daily_variable", en ? "Typical day of spending" : "Typowy dzień wydatków", -Zl.Of(f.DailyVariableGr)),
            new("forecast.projected_end", en ? "Expected on payday" : "Prognoza na dzień wypłaty", Zl.Of(f.ProjectedEndGr)),
        ];
    }
}

// ---------------------------------------------------------------------------- opportunities

/// <param name="TitleKey">i18n key for the frontend, e.g. <c>opportunity.food_delivery</c>.</param>
/// <param name="Title">Template title in the user's language.</param>
/// <param name="Rationale">Explanation; AI-phrased when <paramref name="AiGenerated"/>, otherwise a template.</param>
public sealed record OpportunityDto(
    string Id,
    OpportunityType Type,
    string TitleKey,
    string Title,
    string Rationale,
    decimal MonthlySaving,
    decimal YearlySaving,
    Difficulty Difficulty,
    EvidenceDto Evidence,
    bool AiGenerated)
{
    public static OpportunityDto From(Opportunity o, string language, string? aiRationale = null)
    {
        var en = CoachTexts.IsEnglish(language);
        return new OpportunityDto(
            o.Id,
            o.Type,
            o.TitleKey,
            CoachTexts.OpportunityTitle(o, language),
            aiRationale ?? CoachTexts.OpportunityRationale(o, language),
            Zl.Of(o.MonthlySavingGr),
            Zl.Of(o.YearlySavingGr),
            o.Difficulty,
            new EvidenceDto(o.TransactionIds,
            [
                new("opportunity.monthly_spend", en ? "Spent per month" : "Wydatki miesięcznie", Zl.Of(o.MonthlySpendGr)),
                new("opportunity.monthly_saving", en ? "Possible saving per month" : "Możliwa oszczędność miesięcznie", Zl.Of(o.MonthlySavingGr)),
            ]),
            aiRationale is not null);
    }
}

public sealed record OpportunitiesResponse(decimal TotalMonthlySaving, decimal TotalYearlySaving, IReadOnlyList<OpportunityDto> Items);

// ---------------------------------------------------------------------------- goals

public sealed record GoalDto(
    Guid Id,
    string Name,
    string? Emoji,
    decimal Target,
    decimal Saved,
    decimal Remaining,
    decimal ProgressPct,
    DateOnly? Deadline,
    decimal MonthlyPlan,
    decimal? RequiredPerWeek,
    decimal? RequiredPerMonth,
    GoalStatus Status,
    decimal? ShortBy,
    DateOnly? ReachDate)
{
    public static GoalDto From(GoalProgress p) => new(
        p.Goal.Id,
        p.Goal.Name,
        p.Goal.Emoji,
        Zl.Of(p.Goal.TargetGr),
        Zl.Of(p.Goal.SavedGr),
        Zl.Of(p.RemainingGr),
        p.ProgressPct,
        p.Goal.Deadline,
        Zl.Of(p.Goal.MonthlyPlanGr),
        Zl.Of(p.RequiredPerWeekGr),
        Zl.Of(p.RequiredPerMonthGr),
        p.Status,
        Zl.Of(p.ShortByGr),
        p.ReachDate);
}

/// <param name="MonthlyPlan">Omit to plan what the deadline requires.</param>
public sealed record CreateGoalRequest(string? Name, string? Emoji, decimal? Target, decimal? Saved, DateOnly? Deadline, decimal? MonthlyPlan);

/// <param name="ClearDeadline">True removes the deadline.</param>
public sealed record UpdateGoalRequest(string? Name, string? Emoji, decimal? Target, decimal? Saved, DateOnly? Deadline, bool? ClearDeadline, decimal? MonthlyPlan);

/// <param name="Amount">Positive to add, negative to withdraw.</param>
public sealed record GoalDepositRequest(decimal? Amount);

public sealed record GoalPreviewRequest(decimal? Target, decimal? Saved, DateOnly? Deadline);

public sealed record GoalPlanStepDto(string OpportunityId, string Title, decimal MonthlySaving);

/// <param name="MonthlySurplus">Average monthly income minus expenses over the last 90 days.</param>
/// <param name="Verdict"><c>green</c> if the surplus covers the goal, <c>yellow</c> if savings opportunities close the gap, <c>red</c> otherwise.</param>
/// <param name="Text">Template sentence explaining the verdict, in the user's language.</param>
public sealed record GoalPreviewResponse(
    decimal RequiredPerWeek,
    decimal RequiredPerMonth,
    decimal MonthlySurplus,
    Verdict Verdict,
    IReadOnlyList<GoalPlanStepDto> Plan,
    string Text);

// ---------------------------------------------------------------------------- simulations

/// <param name="Amount">Price in złoty, positive.</param>
/// <param name="Date">Purchase date; defaults to the day after the latest transaction.</param>
/// <param name="Assumptions">What-if overrides for this calculation only (not saved); omitted fields use the profile.</param>
public sealed record SimulatePurchaseRequest(decimal? Amount, DateOnly? Date, string? Item, AssumptionsRequest? Assumptions);

public sealed record AssumptionsRequest(int? Payday, decimal? SafetyBuffer, decimal? Balance);

public sealed record ForecastBriefDto(decimal ProjectedEnd, ForecastStatus Status, DateOnly? RunOutDate, decimal SafeToSpend)
{
    public static ForecastBriefDto From(Forecast f) => new(Zl.Of(f.ProjectedEndGr), f.Status, f.RunOutDate, Zl.Of(f.SafeToSpendGr));
}

public sealed record GoalImpactDto(Guid GoalId, string Name, DateOnly? ReachDateBefore, DateOnly? ReachDateAfter, int ShiftMonths)
{
    public static GoalImpactDto From(GoalImpact g) => new(g.GoalId, g.Name, g.ReachDateBefore, g.ReachDateAfter, g.ShiftMonths);
}

public sealed record AssumptionsDto(int? Payday, decimal SafetyBuffer, decimal Balance);

/// <param name="Explanation">Deterministic template sentence in the user's language.</param>
public sealed record SimulatePurchaseResponse(
    string? Item,
    decimal Amount,
    DateOnly Date,
    bool BeforePayday,
    Verdict Verdict,
    decimal SafeToSpend,
    decimal? LeftAfter,
    decimal? Shortfall,
    ForecastBriefDto Before,
    ForecastBriefDto After,
    IReadOnlyList<FigureDto> Breakdown,
    IReadOnlyList<GoalImpactDto> GoalDelays,
    AssumptionsDto Assumptions,
    string Explanation,
    EvidenceDto Evidence);

public sealed record SimulateChangeRequest(string? Category, decimal? NewPerWeek);

public sealed record SimulateChangeResponse(
    Category Category,
    decimal CurrentPerWeek,
    decimal CurrentPerMonth,
    decimal NewPerWeek,
    decimal MonthlySaving,
    decimal YearlySaving,
    IReadOnlyList<GoalImpactDto> Goals,
    EvidenceDto Evidence);

// ---------------------------------------------------------------------------- challenges

public sealed record ChallengeDto(
    Guid Id, string Type, string Title, DateOnly StartDate, DateOnly EndDate, long Target, int Streak, long BestStreak, string Status, DateOnly? LastCheckInOn)
{
    public static ChallengeDto From(Challenge c) =>
        new(c.Id, c.Type, c.Title, c.StartDate, c.EndDate, c.Target, c.Streak, c.Progress, c.Status, c.LastCheckInOn);
}

/// <param name="Type"><c>no_delivery</c> or <c>no_taxi</c>.</param>
/// <param name="Days">Streak to reach, 1 to 60; default 7.</param>
public sealed record CreateChallengeRequest(string? Type, int? Days);

/// <param name="Broken">A new breaking transaction reset the streak.</param>
public sealed record CheckInResponse(ChallengeDto Challenge, bool Broken, DateOnly? BreakDate, EvidenceDto Evidence);

// ---------------------------------------------------------------------------- alerts

public sealed record AlertDto(
    string Id, AlertType Type, AlertSeverity Severity, string Title, string Message, DateOnly? Date, decimal? Amount, EvidenceDto Evidence)
{
    public static AlertDto From(Alert alert, string language)
    {
        var (title, message) = CoachTexts.AlertText(alert, language);
        return new AlertDto(alert.Id, alert.Type, alert.Severity, title, message, alert.Date, Zl.Of(alert.AmountGr), new EvidenceDto(alert.TransactionIds, []));
    }
}

// ---------------------------------------------------------------------------- wrapped

public sealed record WrappedMonthDto(string Month, bool IsNew);

public sealed record WrappedMerchantDto(string Name, int Count, decimal Amount, EvidenceDto Evidence);

public sealed record WrappedCategoryDto(Category Category, decimal Amount, int SharePct);

public sealed record WrappedChangeDto(Category Category, decimal From, decimal To, int ChangePct);

public sealed record PersonalityDto(string Key, string Emoji, string Title, string Description, bool AiGenerated);

/// <param name="Type">One of <c>total_spent</c>, <c>top_categories</c>, <c>top_merchant</c>, <c>delivery</c>, <c>biggest_day</c>, <c>subscriptions</c>, <c>month_over_month</c>, <c>personality</c>.</param>
/// <param name="Caption">AI-written when <paramref name="AiGenerated"/> (fact-checked), otherwise a template.</param>
public sealed record WrappedCardDto(string Type, string Title, string Caption, IReadOnlyList<FigureDto> Figures, EvidenceDto Evidence, bool AiGenerated);

/// <summary>Captions for the story slides; AI-written (fact-checked) when available.</summary>
public sealed record WrappedCaptionsDto(string TotalSpent, string TopMerchant, string BiggestChange);

public sealed record WrappedResponse(
    string Month,
    decimal TotalSpent,
    decimal TotalIncome,
    int? ChangePct,
    int TransactionCount,
    WrappedMerchantDto? TopMerchant,
    WrappedCategoryDto? TopCategory,
    IReadOnlyList<WrappedCategoryDto> TopCategories,
    WrappedChangeDto? BiggestChange,
    string CheapestWeekday,
    decimal PotentialSavings,
    PersonalityDto Personality,
    WrappedCaptionsDto Captions,
    IReadOnlyList<WrappedCardDto> Cards);

// ---------------------------------------------------------------------------- dashboard

public sealed record DashboardUserDto(string Name, Persona Persona, string Language);

public sealed record MonthTotalsDto(string Month, decimal Income, decimal Expenses, decimal Saved, IReadOnlyList<WrappedCategoryDto> TopCategories);

public sealed record SubscriptionsBriefDto(decimal MonthlyTotal, int Count, int Duplicates);

public sealed record BnplBriefDto(int ActivePlans, decimal TotalRemaining, DateOnly? NextDate, decimal? NextAmount);

public sealed record DashboardResponse(
    DashboardUserDto User,
    DateOnly AsOf,
    ForecastResponse Forecast,
    MonthTotalsDto Month,
    IReadOnlyList<OpportunityDto> Opportunities,
    decimal TotalMonthlySaving,
    IReadOnlyList<GoalDto> Goals,
    IReadOnlyList<AlertDto> Alerts,
    SubscriptionsBriefDto Subscriptions,
    BnplBriefDto Bnpl);

// ---------------------------------------------------------------------------- home

public sealed record PayPeriodDto(DateOnly Start, DateOnly End, int DaysLeft);

/// <param name="Amount">Negative (money spent).</param>
/// <param name="ChangePct">Against the previous month; <c>null</c> when nothing was spent then.</param>
public sealed record HomeCategoryDto(Category Category, decimal Amount, decimal? ChangePct);

/// <param name="Amount">Negative (a payment).</param>
/// <param name="Period"><c>weekly</c> or <c>monthly</c>.</param>
public sealed record HomeRecurringDto(string Merchant, RecurringType Type, decimal Amount, string Period, DateOnly? NextDate, IReadOnlyList<Guid> TransactionIds);

public sealed record NarrativeBulletDto(string Text, IReadOnlyList<string> FactKeys, IReadOnlyList<Guid> TransactionIds);

/// <param name="FactCheck"><c>passed</c> when every AI text was verified, <c>fallback</c> when templates were used.</param>
public sealed record NarrativeDto(string Headline, IReadOnlyList<NarrativeBulletDto> Bullets, bool AiGenerated, string FactCheck);

/// <summary>The Home screen for one month; safe-to-spend and pay period are always "now".</summary>
/// <param name="Expenses">Negative.</param>
/// <param name="Saved">Income plus expenses.</param>
public sealed record HomeResponse(
    string Month,
    DateOnly AsOf,
    decimal Income,
    decimal Expenses,
    decimal Saved,
    int? ExpensesChangePct,
    decimal SafeToSpend,
    EvidenceDto SafeToSpendEvidence,
    PayPeriodDto PayPeriod,
    ForecastStatus Status,
    IReadOnlyList<HomeCategoryDto> ByCategory,
    IReadOnlyList<HomeRecurringDto> Recurring,
    NarrativeDto Narrative);

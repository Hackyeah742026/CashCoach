using System.Globalization;
using System.Text.Json;
using CashCoach.Core.Abstractions;
using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Analytics;

namespace CashCoach.Infrastructure.Ai.Tools;

/// <param name="Key">Stable fact key, e.g. <c>forecast.safe_to_spend</c>.</param>
public sealed record ToolFigure(string Key, string Label, long AmountGr);

/// <param name="Result">Serialized to JSON for the model; every number in it is an allowed fact.</param>
public sealed record ToolOutput(object Result, IReadOnlyList<Guid> TransactionIds, IReadOnlyList<ToolFigure> Figures);

/// <summary>Per-turn state: the user is fixed by the server, never chosen by the model.</summary>
public sealed class ToolContext(Guid userId, string language)
{
    public Guid UserId { get; } = userId;
    public string Language { get; } = language;
    internal FinancialSnapshot? Snapshot { get; set; }
}

/// <summary>The chat tools: JSON schemas for the model plus C# implementations that call the Core calculators.</summary>
public sealed class ToolRegistry(SnapshotLoader loader, AnalyticsService analytics, GoalService goals)
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private const int MaxListedTransactions = 20;
    private const int MaxEvidenceIds = 50;

    private static readonly string Categories = string.Join(", ", SnakeCaseEnum<Category>.AllNames.Select(name => $"\"{name}\""));

    public static IReadOnlyList<LlmToolDefinition> Definitions { get; } =
    [
        new("get_balance", "Current balance, safe-to-spend until payday, safety buffer and next payday.", """{"type":"object","properties":{}}"""),
        new("get_spending", "Spending totals for a period, optionally for one category or merchant. Defaults to the current month.",
            """{"type":"object","properties":{"month":{"type":"string","description":"YYYY-MM"},"from":{"type":"string","description":"YYYY-MM-DD"},"to":{"type":"string","description":"YYYY-MM-DD"},"category":{"type":"string","enum":[CATEGORIES]},"merchant":{"type":"string"}}}""".Replace("CATEGORIES", Categories)),
        new("get_upcoming_payments", "Bills, subscriptions, rent and BNPL instalments due before the next payday.", """{"type":"object","properties":{}}"""),
        new("forecast_until_payday", "Day-by-day balance forecast until payday: projected end balance, run-out date, status ok/tight/danger, typical daily spending.", """{"type":"object","properties":{}}"""),
        new("simulate_purchase", "Can the user afford a purchase? Returns verdict green/yellow/red, safe-to-spend before and after, shortfall and goal delays.",
            """{"type":"object","properties":{"amount":{"type":"number","description":"Price in zł"},"date":{"type":"string","description":"YYYY-MM-DD, optional"}},"required":["amount"]}"""),
        new("simulate_change", "Saving from spending a new amount per week in a category, and how goals move.",
            """{"type":"object","properties":{"category":{"type":"string","enum":[CATEGORIES]},"new_per_week":{"type":"number","description":"Planned weekly spending in zł"}},"required":["category","new_per_week"]}""".Replace("CATEGORIES", Categories)),
        new("list_subscriptions", "Active subscriptions with monthly cost and overlapping (duplicate) services.", """{"type":"object","properties":{}}"""),
        new("get_goals", "Savings goals with progress, status, required saving per week and reach date.", """{"type":"object","properties":{}}"""),
        new("create_goal", "Create a savings goal. Only when the user explicitly asks for it.",
            """{"type":"object","properties":{"name":{"type":"string"},"target":{"type":"number","description":"zł"},"deadline":{"type":"string","description":"YYYY-MM-DD, optional"},"monthly_plan":{"type":"number","description":"zł per month, optional"}},"required":["name","target"]}"""),
        new("get_savings_opportunities", "Computed savings ideas with monthly and yearly savings.", """{"type":"object","properties":{}}"""),
    ];

    public async Task<ToolOutput> ExecuteAsync(string name, string argumentsJson, ToolContext context, CancellationToken cancellationToken)
    {
        JsonElement args;
        try
        {
            args = JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return Error("Arguments are not valid JSON.");
        }

        try
        {
            var snapshot = context.Snapshot ??= await loader.LoadAsync(context.UserId, cancellationToken);
            return name switch
            {
                "get_balance" => Balance(snapshot, context.Language),
                "get_spending" => Spending(snapshot, args),
                "get_upcoming_payments" => Upcoming(snapshot),
                "forecast_until_payday" => ForecastTool(snapshot, context.Language),
                "simulate_purchase" => Purchase(snapshot, args, context.Language),
                "simulate_change" => Change(snapshot, args),
                "list_subscriptions" => Subscriptions(snapshot),
                "get_goals" => Goals(snapshot),
                "create_goal" => await CreateGoalAsync(snapshot, args, context, cancellationToken),
                "get_savings_opportunities" => await OpportunitiesAsync(snapshot, context, cancellationToken),
                _ => Error($"Unknown tool '{name}'."),
            };
        }
        catch (ToolArgumentException exception)
        {
            return Error(exception.Message);
        }
    }

    private static ToolOutput Balance(FinancialSnapshot s, string language)
    {
        var f = ForecastCalculator.Compute(s);
        return new ToolOutput(
            new
            {
                as_of = f.AsOf,
                balance = Zl(f.BalanceGr),
                safe_to_spend = Zl(f.SafeToSpendGr),
                safe_per_day = Zl(f.SafePerDayGr),
                safety_buffer = Zl(f.SafetyBufferGr),
                bills_before_payday = Zl(f.FixedUpcomingGr),
                next_payday = f.NextPayday,
                days_left = f.DaysLeft,
            },
            f.UpcomingTransactionIds,
            ForecastFigures(f, language).Take(4).ToList());
    }

    private static ToolOutput ForecastTool(FinancialSnapshot s, string language)
    {
        var f = ForecastCalculator.Compute(s);
        return new ToolOutput(
            new
            {
                as_of = f.AsOf,
                next_payday = f.NextPayday,
                days_left = f.DaysLeft,
                balance = Zl(f.BalanceGr),
                bills_before_payday = Zl(f.FixedUpcomingGr),
                typical_daily_spending = Zl(f.DailyVariableGr),
                projected_balance_on_payday = Zl(f.ProjectedEndGr),
                run_out_date = f.RunOutDate,
                status = SnakeCaseEnum<ForecastStatus>.ToName(f.Status),
                safe_to_spend = Zl(f.SafeToSpendGr),
            },
            f.UpcomingTransactionIds,
            ForecastFigures(f, language).ToList());
    }

    private static ToolOutput Spending(FinancialSnapshot s, JsonElement args)
    {
        DateOnly from, to;
        if (Str(args, "month") is { } month)
        {
            if (!DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out from))
            {
                throw new ToolArgumentException("'month' must be YYYY-MM.");
            }

            to = from.AddMonths(1).AddDays(-1);
        }
        else
        {
            from = Date(args, "from") ?? new DateOnly(s.AsOf.Year, s.AsOf.Month, 1);
            to = Date(args, "to") ?? s.AsOf;
        }

        Category? category = null;
        if (Str(args, "category") is { } categoryName)
        {
            category = SnakeCaseEnum<Category>.TryParse(categoryName, out var parsed)
                ? parsed
                : throw new ToolArgumentException($"Unknown category '{categoryName}'.");
        }

        var merchant = Str(args, "merchant");
        var expenses = s.ExpensesBetween(from, to)
            .Where(t => category is null || t.Category == category)
            .Where(t => merchant is null || t.Merchant.Contains(merchant, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Date)
            .ToList();
        var total = -expenses.Sum(t => t.AmountGr);

        return new ToolOutput(
            new
            {
                from,
                to,
                category = category is { } c ? SnakeCaseEnum<Category>.ToName(c) : null,
                merchant,
                total_spent = Zl(total),
                count = expenses.Count,
                by_category = expenses.GroupBy(t => t.Category)
                    .Select(g => new { category = SnakeCaseEnum<Category>.ToName(g.Key), amount = Zl(-g.Sum(t => t.AmountGr)), count = g.Count() })
                    .OrderByDescending(g => g.amount).ToList(),
                top_merchants = expenses.GroupBy(t => t.Merchant)
                    .Select(g => new { merchant = g.Key, amount = Zl(-g.Sum(t => t.AmountGr)), count = g.Count() })
                    .OrderByDescending(g => g.amount).Take(5).ToList(),
                transactions = expenses.Take(MaxListedTransactions)
                    .Select(t => new { date = t.Date, merchant = t.Merchant, category = SnakeCaseEnum<Category>.ToName(t.Category), amount = Zl(-t.AmountGr) })
                    .ToList(),
            },
            expenses.Take(MaxEvidenceIds).Select(t => t.Id).ToList(),
            [new ToolFigure("spending.total", $"{merchant ?? (category is { } cat ? SnakeCaseEnum<Category>.ToName(cat) : "all")} {from:yyyy-MM-dd}…{to:yyyy-MM-dd}", -total)]);
    }

    private static ToolOutput Upcoming(FinancialSnapshot s)
    {
        var f = ForecastCalculator.Compute(s);
        return new ToolOutput(
            new
            {
                next_payday = f.NextPayday,
                total = Zl(f.FixedUpcomingGr),
                payments = f.Upcoming.Select(p => new { date = p.Date, merchant = p.Merchant, type = SnakeCaseEnum<RecurringType>.ToName(p.Type), amount = Zl(p.AmountGr) }).ToList(),
            },
            f.UpcomingTransactionIds,
            f.Upcoming.Select(p => new ToolFigure($"upcoming.{p.Merchant}", $"{p.Merchant} {p.Date:yyyy-MM-dd}", -p.AmountGr)).ToList());
    }

    private static ToolOutput Purchase(FinancialSnapshot s, JsonElement args, string language)
    {
        var amount = Grosze(args, "amount") ?? throw new ToolArgumentException("'amount' is required.");
        if (amount <= 0)
        {
            throw new ToolArgumentException("'amount' must be positive.");
        }

        var sim = Simulator.SimulatePurchase(s, amount, Date(args, "date"));
        return new ToolOutput(
            new
            {
                amount = Zl(sim.AmountGr),
                date = sim.Date,
                verdict = SnakeCaseEnum<Verdict>.ToName(sim.Verdict),
                safe_to_spend = Zl(sim.Before.SafeToSpendGr),
                left_after = sim.LeftAfterGr is { } left ? Zl(left) : (decimal?)null,
                shortfall = sim.ShortfallGr is { } shortfall ? Zl(shortfall) : (decimal?)null,
                safety_buffer = Zl(s.SafetyBufferGr),
                next_payday = sim.Before.NextPayday,
                projected_balance_on_payday_before = Zl(sim.Before.ProjectedEndGr),
                projected_balance_on_payday_after = Zl(sim.After.ProjectedEndGr),
                status_after = SnakeCaseEnum<ForecastStatus>.ToName(sim.After.Status),
                run_out_date_after = sim.After.RunOutDate,
                goal_delays = sim.GoalDelays.Select(g => new { goal = g.Name, reach_date_before = g.ReachDateBefore, reach_date_after = g.ReachDateAfter, delay_months = g.ShiftMonths }).ToList(),
            },
            sim.Before.UpcomingTransactionIds,
            [.. ForecastFigures(sim.Before, language).Take(4), new ToolFigure("purchase.amount", language == "en" ? "Purchase" : "Zakup", -sim.AmountGr)]);
    }

    private static ToolOutput Change(FinancialSnapshot s, JsonElement args)
    {
        var categoryName = Str(args, "category") ?? throw new ToolArgumentException("'category' is required.");
        if (!SnakeCaseEnum<Category>.TryParse(categoryName, out var category))
        {
            throw new ToolArgumentException($"Unknown category '{categoryName}'.");
        }

        var perWeek = Grosze(args, "new_per_week") ?? throw new ToolArgumentException("'new_per_week' is required.");
        var change = Simulator.SimulateChange(s, category, Math.Max(0, perWeek));
        return new ToolOutput(
            new
            {
                category = categoryName,
                current_per_week = Zl(change.CurrentPerWeekGr),
                current_per_month = Zl(change.CurrentPerMonthGr),
                new_per_week = Zl(change.NewPerWeekGr),
                monthly_saving = Zl(change.MonthlySavingGr),
                yearly_saving = Zl(change.YearlySavingGr),
                goals = change.Goals.Select(g => new { goal = g.Name, reach_date_before = g.ReachDateBefore, reach_date_after = g.ReachDateAfter, months_sooner = -g.ShiftMonths }).ToList(),
            },
            change.TransactionIds.Take(MaxEvidenceIds).ToList(),
            [new ToolFigure("change.monthly_saving", $"{categoryName}: monthly saving", change.MonthlySavingGr)]);
    }

    private static ToolOutput Subscriptions(FinancialSnapshot s)
    {
        var overview = AnalyticsService.Subscriptions(s);
        var ids = overview.Items.Select(i => i.Subscription.Id).ToHashSet();
        return new ToolOutput(
            new
            {
                monthly_total = Zl(overview.MonthlyTotalGr),
                count = overview.Items.Count,
                items = overview.Items.Select(i => new
                {
                    merchant = i.Subscription.Merchant,
                    amount = Zl(i.AmountGr),
                    next_date = i.Subscription.NextDate,
                    group = i.Group,
                    duplicate = i.Duplicate,
                    still_using = i.Subscription.UserConfirmed,
                }).ToList(),
            },
            s.Transactions.Where(t => t.RecurringGroupId is { } id && ids.Contains(id)).OrderByDescending(t => t.Date).Select(t => t.Id).Take(MaxEvidenceIds).ToList(),
            [new ToolFigure("subscriptions.monthly_total", "Subscriptions / month", -overview.MonthlyTotalGr)]);
    }

    private static ToolOutput Goals(FinancialSnapshot s) => new(
        new { goals = s.Goals.Select(g => GoalFacts(GoalCalculator.Progress(g, s.AsOf))).ToList() },
        [],
        s.Goals.Select(g => new ToolFigure($"goal.{g.Name}", g.Name, g.SavedGr)).ToList());

    private async Task<ToolOutput> CreateGoalAsync(FinancialSnapshot s, JsonElement args, ToolContext context, CancellationToken cancellationToken)
    {
        var name = Str(args, "name")?.Trim() is { Length: > 0 and <= 60 } n ? n : throw new ToolArgumentException("'name' must be 1 to 60 characters.");
        var target = Grosze(args, "target") is { } t and > 0 ? t : throw new ToolArgumentException("'target' must be positive.");
        var deadline = Date(args, "deadline");
        if (deadline is { } d && d <= s.AsOf)
        {
            throw new ToolArgumentException($"'deadline' must be after {s.AsOf:yyyy-MM-dd}.");
        }

        var goal = await goals.CreateAsync(context.UserId, new GoalInput(name, null, target, 0, deadline, Grosze(args, "monthly_plan")), s.AsOf, cancellationToken);
        context.Snapshot = null;
        return new ToolOutput(new { created = GoalFacts(GoalCalculator.Progress(goal, s.AsOf)) }, [], [new ToolFigure($"goal.{goal.Name}", goal.Name, goal.TargetGr)]);
    }

    private async Task<ToolOutput> OpportunitiesAsync(FinancialSnapshot s, ToolContext context, CancellationToken cancellationToken)
    {
        var opportunities = await analytics.OpportunitiesAsync(s, context.UserId, cancellationToken);
        return new ToolOutput(
            new
            {
                total_monthly_saving = Zl(opportunities.Sum(o => o.MonthlySavingGr)),
                items = opportunities.Select(o => new
                {
                    id = o.Id,
                    idea = CoachTexts.OpportunityTitle(o, context.Language),
                    about = o.Subject,
                    monthly_spend = Zl(o.MonthlySpendGr),
                    monthly_saving = Zl(o.MonthlySavingGr),
                    yearly_saving = Zl(o.YearlySavingGr),
                    payments_per_month = o.Count,
                    difficulty = SnakeCaseEnum<Difficulty>.ToName(o.Difficulty),
                }).ToList(),
            },
            opportunities.SelectMany(o => o.TransactionIds).Distinct().Take(MaxEvidenceIds).ToList(),
            opportunities.Select(o => new ToolFigure($"opportunity.{o.Id}", CoachTexts.OpportunityTitle(o, context.Language), o.MonthlySavingGr)).ToList());
    }

    private static object GoalFacts(GoalProgress p) => new
    {
        name = p.Goal.Name,
        target = Zl(p.Goal.TargetGr),
        saved = Zl(p.Goal.SavedGr),
        remaining = Zl(p.RemainingGr),
        progress_pct = p.ProgressPct,
        deadline = p.Goal.Deadline,
        monthly_plan = Zl(p.Goal.MonthlyPlanGr),
        required_per_week = p.RequiredPerWeekGr is { } w ? Zl(w) : (decimal?)null,
        status = SnakeCaseEnum<GoalStatus>.ToName(p.Status),
        short_by = p.ShortByGr is { } sb ? Zl(sb) : (decimal?)null,
        reach_date = p.ReachDate,
    };

    private static IEnumerable<ToolFigure> ForecastFigures(Forecast f, string language)
    {
        var en = language == "en";
        yield return new("forecast.balance", en ? "Current balance" : "Saldo konta", f.BalanceGr);
        yield return new("forecast.fixed_upcoming", en ? "Bills before payday" : "Rachunki przed wypłatą", -f.FixedUpcomingGr);
        yield return new("forecast.safety_buffer", en ? "Safety buffer" : "Poduszka bezpieczeństwa", -f.SafetyBufferGr);
        yield return new("forecast.safe_to_spend", en ? "Safe to spend" : "Bezpiecznie do wydania", f.SafeToSpendGr);
        yield return new("forecast.projected_end", en ? "Expected on payday" : "Prognoza na dzień wypłaty", f.ProjectedEndGr);
    }

    private static ToolOutput Error(string message) => new(new { error = message }, [], []);

    private static decimal Zl(long grosze) => Money.ToZloty(grosze);

    private static string? Str(JsonElement args, string name) =>
        args.ValueKind == JsonValueKind.Object && args.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text
            : null;

    private static DateOnly? Date(JsonElement args, string name) => Str(args, name) is { } text
        ? DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ToolArgumentException($"'{name}' must be YYYY-MM-DD.")
        : null;

    private static long? Grosze(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
        {
            return null;
        }

        var zloty = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal(),
            JsonValueKind.String when decimal.TryParse(value.GetString()?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => throw new ToolArgumentException($"'{name}' must be a number."),
        };
        return Money.Round(zloty * 100);
    }

    private sealed class ToolArgumentException(string message) : Exception(message);
}

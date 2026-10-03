using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Analytics;
using CashCoach.Infrastructure.Analytics;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class GoalEndpoints
{
    private const int MaxNameLength = 60;
    private const decimal MaxTarget = 10_000_000m;

    public static RouteGroupBuilder MapGoalEndpoints(this RouteGroupBuilder api)
    {
        var goals = api.MapGroup("/goals").WithTags("Goals");

        goals.MapGet("", async (CurrentUser user, SnapshotLoader loader, CancellationToken ct) =>
            {
                var snapshot = await loader.LoadAsync(user.Id, ct);
                return TypedResults.Ok(snapshot.Goals.Select(g => GoalDto.From(GoalCalculator.Progress(g, snapshot.AsOf))).ToList());
            })
            .WithName("ListGoals");

        goals.MapPost("", async (CreateGoalRequest request, CurrentUser user, SnapshotLoader loader, GoalService service, CancellationToken ct) =>
            {
                var name = ValidName(request.Name) ?? throw BadRequest("invalid_name", $"'name' must be 1 to {MaxNameLength} characters.");
                var target = ToGrosze(request.Target, "target", 0.01m, MaxTarget);
                var saved = request.Saved is null ? 0 : ToGrosze(request.Saved, "saved", 0m, MaxTarget);
                long? plan = request.MonthlyPlan is null ? null : ToGrosze(request.MonthlyPlan, "monthly_plan", 0m, MaxTarget);
                var snapshot = await loader.LoadAsync(user.Id, ct);
                ValidDeadline(request.Deadline, snapshot.AsOf);

                var goal = await service.CreateAsync(user.Id, new GoalInput(name, request.Emoji, target, saved, request.Deadline, plan), snapshot.AsOf, ct);
                return TypedResults.Created($"/api/goals/{goal.Id}", GoalDto.From(GoalCalculator.Progress(goal, snapshot.AsOf)));
            })
            .WithName("CreateGoal");

        async Task<GoalDto> Update(Guid id, UpdateGoalRequest request, CurrentUser user, SnapshotLoader loader, GoalService service, CancellationToken ct)
        {
            var name = request.Name is null ? null : ValidName(request.Name) ?? throw BadRequest("invalid_name", $"'name' must be 1 to {MaxNameLength} characters.");
            long? target = request.Target is null ? null : ToGrosze(request.Target, "target", 0.01m, MaxTarget);
            long? saved = request.Saved is null ? null : ToGrosze(request.Saved, "saved", 0m, MaxTarget);
            long? plan = request.MonthlyPlan is null ? null : ToGrosze(request.MonthlyPlan, "monthly_plan", 0m, MaxTarget);
            var snapshot = await loader.LoadAsync(user.Id, ct);
            ValidDeadline(request.Deadline, snapshot.AsOf);

            var patch = new GoalPatch(name, request.Emoji, target, saved, request.Deadline, request.ClearDeadline ?? false, plan);
            var goal = await service.UpdateAsync(user.Id, id, patch, ct) ?? throw NotFound("goal_not_found", "Goal not found.");
            return GoalDto.From(GoalCalculator.Progress(goal, snapshot.AsOf));
        }

        goals.MapPatch("/{id:guid}", async (Guid id, UpdateGoalRequest request, CurrentUser user, SnapshotLoader loader, GoalService service, CancellationToken ct) =>
                TypedResults.Ok(await Update(id, request, user, loader, service, ct)))
            .WithName("UpdateGoal");

        // Same as PATCH; the frontend's goal sheet sends PUT.
        goals.MapPut("/{id:guid}", async (Guid id, UpdateGoalRequest request, CurrentUser user, SnapshotLoader loader, GoalService service, CancellationToken ct) =>
                TypedResults.Ok(await Update(id, request, user, loader, service, ct)))
            .WithName("ReplaceGoal");

        goals.MapDelete("/{id:guid}", async (Guid id, CurrentUser user, GoalService service, CancellationToken ct) =>
                await service.DeleteAsync(user.Id, id, ct) ? TypedResults.NoContent() : throw NotFound("goal_not_found", "Goal not found."))
            .WithName("DeleteGoal");

        goals.MapPost("/{id:guid}/deposit", async (Guid id, GoalDepositRequest request, CurrentUser user, SnapshotLoader loader, GoalService service, CancellationToken ct) =>
            {
                var amount = ToGrosze(request.Amount, "amount", -MaxTarget, MaxTarget);
                if (amount == 0)
                {
                    throw BadRequest("invalid_amount", "'amount' must not be zero.");
                }

                var goal = await service.DepositAsync(user.Id, id, amount, ct) ?? throw NotFound("goal_not_found", "Goal not found.");
                var snapshot = await loader.LoadAsync(user.Id, ct);
                return TypedResults.Ok(GoalDto.From(GoalCalculator.Progress(goal, snapshot.AsOf)));
            })
            .WithName("DepositToGoal");

        goals.MapPost("/preview", async (GoalPreviewRequest request, CurrentUser user, SnapshotLoader loader, AnalyticsService analytics, CancellationToken ct) =>
            {
                var target = ToGrosze(request.Target, "target", 0.01m, MaxTarget);
                var saved = request.Saved is null ? 0 : ToGrosze(request.Saved, "saved", 0m, MaxTarget);
                var deadline = request.Deadline ?? throw BadRequest("invalid_deadline", "'deadline' is required.");
                var snapshot = await loader.LoadAsync(user.Id, ct);
                ValidDeadline(deadline, snapshot.AsOf);

                var preview = GoalPlanner.Preview(snapshot, target, saved, deadline, await analytics.OpportunitiesAsync(snapshot, user.Id, ct));
                return TypedResults.Ok(new GoalPreviewResponse(
                    Zl.Of(preview.RequiredPerWeekGr),
                    Zl.Of(preview.RequiredPerMonthGr),
                    Zl.Of(preview.MonthlySurplusGr),
                    preview.Verdict,
                    preview.Plan.Select(o => new GoalPlanStepDto(o.Id, CoachTexts.OpportunityTitle(o, user.User.Language), Zl.Of(o.MonthlySavingGr))).ToList(),
                    CoachTexts.GoalPreviewText(preview, user.User.Language)));
            })
            .WithName("PreviewGoal");

        return api;
    }

    private static string? ValidName(string? name) =>
        name?.Trim() is { Length: > 0 and <= MaxNameLength } trimmed ? trimmed : null;

    private static void ValidDeadline(DateOnly? deadline, DateOnly asOf)
    {
        if (deadline is { } date && date <= asOf)
        {
            throw BadRequest("invalid_deadline", $"'deadline' must be after {asOf:yyyy-MM-dd}.");
        }
    }
}

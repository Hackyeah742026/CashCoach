using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Analytics;

/// <param name="MonthlyPlanGr">When <c>null</c> on create, the plan is what the deadline requires (or 0 without a deadline).</param>
public sealed record GoalInput(string Name, string? Emoji, long TargetGr, long SavedGr, DateOnly? Deadline, long? MonthlyPlanGr);

/// <summary>Fields to change; <c>null</c> keeps the current value. <see cref="ClearDeadline"/> removes the deadline.</summary>
public sealed record GoalPatch(string? Name, string? Emoji, long? TargetGr, long? SavedGr, DateOnly? Deadline, bool ClearDeadline, long? MonthlyPlanGr);

public sealed class GoalService(AppDbContext db, TimeProvider timeProvider)
{
    public async Task<List<Goal>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Goals.AsNoTracking().Where(g => g.UserId == userId).OrderBy(g => g.CreatedAt).ToListAsync(cancellationToken);

    public async Task<Goal> CreateAsync(Guid userId, GoalInput input, DateOnly asOf, CancellationToken cancellationToken)
    {
        var remaining = Math.Max(0, input.TargetGr - input.SavedGr);
        var goal = new Goal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = input.Name,
            Emoji = input.Emoji,
            TargetGr = input.TargetGr,
            SavedGr = input.SavedGr,
            Deadline = input.Deadline,
            MonthlyPlanGr = input.MonthlyPlanGr
                ?? (input.Deadline is { } deadline ? GoalCalculator.RequiredPerMonth(asOf, deadline, remaining) : 0),
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        db.Goals.Add(goal);
        await db.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task<Goal?> UpdateAsync(Guid userId, Guid goalId, GoalPatch patch, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal is null)
        {
            return null;
        }

        goal.Name = patch.Name ?? goal.Name;
        goal.Emoji = patch.Emoji ?? goal.Emoji;
        goal.TargetGr = patch.TargetGr ?? goal.TargetGr;
        goal.SavedGr = patch.SavedGr ?? goal.SavedGr;
        goal.Deadline = patch.ClearDeadline ? null : patch.Deadline ?? goal.Deadline;
        goal.MonthlyPlanGr = patch.MonthlyPlanGr ?? goal.MonthlyPlanGr;
        await db.SaveChangesAsync(cancellationToken);
        return goal;
    }

    /// <param name="amountGr">Positive to add savings, negative to withdraw; the result never goes below zero.</param>
    public async Task<Goal?> DepositAsync(Guid userId, Guid goalId, long amountGr, CancellationToken cancellationToken)
    {
        var goal = await db.Goals.SingleOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal is null)
        {
            return null;
        }

        goal.SavedGr = Math.Max(0, goal.SavedGr + amountGr);
        await db.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid goalId, CancellationToken cancellationToken) =>
        await db.Goals.Where(g => g.Id == goalId && g.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;
}

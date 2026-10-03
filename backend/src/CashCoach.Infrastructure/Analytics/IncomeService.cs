using CashCoach.Core.Analytics;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CashCoach.Infrastructure.Analytics;

/// <param name="Day">1 to 31; ignored (31) with <see cref="PaydayRule.LastWorkingDay"/>.</param>
/// <param name="AmountGr">Monthly income in grosze, positive.</param>
public sealed record IncomeConfirmation(int Day, PaydayRule DayRule, long AmountGr, string? Source);

/// <summary>Regular income: what the imported data suggests, and what the user confirmed.</summary>
public sealed class IncomeService(AppDbContext db)
{
    public async Task<IReadOnlyList<IncomeCandidate>> DetectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var incoming = await db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.AmountGr > 0).ToListAsync(cancellationToken);
        return IncomeDetector.Detect(incoming);
    }

    /// <summary>Saves the user's answer on a user tracked by this context. <c>null</c> means "no regular income".</summary>
    public async Task ConfirmAsync(User user, IncomeConfirmation? income, CancellationToken cancellationToken)
    {
        if (income is null)
        {
            user.IncomeStatus = IncomeStatus.None;
            user.Payday = null;
            user.PaydayRule = PaydayRule.FixedDay;
            user.SalaryGr = null;
            user.IncomeSource = null;
        }
        else
        {
            user.IncomeStatus = IncomeStatus.Confirmed;
            user.Payday = income.DayRule == PaydayRule.LastWorkingDay ? 31 : income.Day;
            user.PaydayRule = income.DayRule;
            user.SalaryGr = income.AmountGr;
            user.IncomeSource = string.IsNullOrWhiteSpace(income.Source) ? null : income.Source.Trim();
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}

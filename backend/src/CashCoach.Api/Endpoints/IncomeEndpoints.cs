using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Domain;
using CashCoach.Infrastructure.Analytics;
using CashCoach.Infrastructure.Users;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class IncomeEndpoints
{
    private const int MaxSourceLength = 60;

    public static RouteGroupBuilder MapIncomeEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/income/detection", async (CurrentUser user, IncomeService income, CancellationToken ct) =>
            {
                var candidates = await income.DetectAsync(user.Id, ct);
                var u = user.User;
                IncomeDto? confirmed = u.IncomeStatus == IncomeStatus.Unknown
                    ? null
                    : new IncomeDto(u.IncomeStatus, u.Payday, u.PaydayRule, u.SalaryGr is { } s ? Money.ToZloty(s) : null, u.IncomeSource);
                return TypedResults.Ok(new IncomeDetectionResponse(
                    candidates.Count > 0 ? IncomeCandidateDto.From(candidates[0]) : null,
                    candidates.Skip(1).Select(IncomeCandidateDto.From).ToList(),
                    confirmed));
            })
            .WithName("DetectIncome")
            .WithTags("Income");

        api.MapPut("/me/income", async (
                ConfirmIncomeRequest request, CurrentUser user, IncomeService income, UserProfileService profiles, CancellationToken ct) =>
            {
                var hasIncome = request.HasIncome ?? throw BadRequest("invalid_has_income", "'has_income' is required.");
                IncomeConfirmation? confirmation = null;
                if (hasIncome)
                {
                    var rule = request.DayRule is null ? PaydayRule.FixedDay : ParseEnum<PaydayRule>(request.DayRule, "day_rule");
                    var day = rule == PaydayRule.LastWorkingDay ? 31 : request.Day ?? 0;
                    if (day is < 1 or > 31)
                    {
                        throw BadRequest("invalid_day", "'day' must be a day of month from 1 to 31.");
                    }

                    var amount = ToGrosze(request.Amount, "amount", 1m, 1_000_000m);
                    if (request.Source?.Trim().Length > MaxSourceLength)
                    {
                        throw BadRequest("invalid_source", $"'source' must be at most {MaxSourceLength} characters.");
                    }

                    confirmation = new IncomeConfirmation(day, rule, amount, request.Source);
                }

                await income.ConfirmAsync(user.User, confirmation, ct);
                return TypedResults.Ok(UserProfileResponse.From(await profiles.GetAsync(user.User, ct)));
            })
            .WithName("ConfirmIncome")
            .WithTags("Income");

        return api;
    }
}

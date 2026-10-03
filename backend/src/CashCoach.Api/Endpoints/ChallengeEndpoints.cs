using CashCoach.Api.Contracts;
using CashCoach.Api.Errors;
using CashCoach.Api.Users;
using CashCoach.Core.Analytics;
using CashCoach.Infrastructure.Analytics;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class ChallengeEndpoints
{
    private const int DefaultDays = 7;
    private const int MaxDays = 60;

    public static RouteGroupBuilder MapChallengeEndpoints(this RouteGroupBuilder api)
    {
        var challenges = api.MapGroup("/challenges").WithTags("Challenges");

        challenges.MapGet("", async (CurrentUser user, ChallengeService service, CancellationToken ct) =>
                TypedResults.Ok((await service.ListAsync(user.Id, ct)).Select(ChallengeDto.From).ToList()))
            .WithName("ListChallenges");

        challenges.MapPost("", async (CreateChallengeRequest request, CurrentUser user, ChallengeService service, CancellationToken ct) =>
            {
                if (request.Type is null || !ChallengeTypes.All.Contains(request.Type))
                {
                    throw BadRequest("invalid_type", $"'type' must be one of: {string.Join(", ", ChallengeTypes.All)}.");
                }

                var days = request.Days ?? DefaultDays;
                if (days is < 1 or > MaxDays)
                {
                    throw BadRequest("invalid_days", $"'days' must be between 1 and {MaxDays}.");
                }

                var challenge = await service.CreateAsync(user.Id, request.Type, days, user.User.Language, ct);
                return TypedResults.Created($"/api/challenges/{challenge.Id}", ChallengeDto.From(challenge));
            })
            .WithName("CreateChallenge");

        challenges.MapPost("/{id:guid}/checkin", async (Guid id, CurrentUser user, ChallengeService service, CancellationToken ct) =>
            {
                try
                {
                    var checkIn = await service.CheckInAsync(user.Id, id, ct) ?? throw NotFound("challenge_not_found", "Challenge not found.");
                    return TypedResults.Ok(new CheckInResponse(
                        ChallengeDto.From(checkIn.Challenge),
                        checkIn.Result.Broken,
                        checkIn.Result.BreakDate,
                        new EvidenceDto(checkIn.Result.BreakingTransactionIds, [])));
                }
                catch (AlreadyCheckedInException)
                {
                    throw new ApiException("already_checked_in", "You already checked in today.", StatusCodes.Status409Conflict);
                }
            })
            .WithName("CheckInChallenge");

        return api;
    }
}

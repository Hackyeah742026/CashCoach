using CashCoach.Api.Contracts;
using CashCoach.Api.Users;
using CashCoach.Core.Services;
using CashCoach.Infrastructure.Insights;
using static CashCoach.Api.Endpoints.RequestValidation;

namespace CashCoach.Api.Endpoints;

public static class InsightEndpoints
{
    public static RouteGroupBuilder MapInsightEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/summary", async (string? period, CurrentUser currentUser, InsightService insights, CancellationToken cancellationToken) =>
            {
                var summaryPeriod = SummaryPeriod.ThisMonth;
                if (period is not null && !SummaryPeriods.TryParse(period, out summaryPeriod))
                {
                    throw BadRequest("invalid_period", "'period' must be one of: this_month, last_month, last_3_months.");
                }

                return TypedResults.Ok(SummaryResponse.From(await insights.GetSummaryAsync(currentUser.Id, summaryPeriod, cancellationToken)));
            })
            .WithName("GetSummary")
            .WithTags("Insights");

        api.MapGet("/subscriptions", async (CurrentUser currentUser, InsightService insights, CancellationToken cancellationToken) =>
                TypedResults.Ok(SubscriptionsResponse.From(await insights.GetSubscriptionsAsync(currentUser.Id, cancellationToken))))
            .WithName("ListSubscriptions")
            .WithTags("Insights");

        api.MapPatch("/subscriptions/{id:guid}", async (
                Guid id, UpdateSubscriptionRequest request,
                CurrentUser currentUser, InsightService insights, CancellationToken cancellationToken) =>
            {
                var stillUsing = request.StillUsing ?? throw BadRequest("invalid_still_using", "'still_using' is required.");
                var subscription = await insights.SetStillUsingAsync(currentUser.Id, id, stillUsing, cancellationToken)
                    ?? throw NotFound("subscription_not_found", "Subscription not found.");
                return TypedResults.Ok(new UpdateSubscriptionResponse(subscription.Id, subscription.UserConfirmed, null));
            })
            .WithName("UpdateSubscription")
            .WithTags("Insights");

        api.MapGet("/bnpl", async (CurrentUser currentUser, InsightService insights, CancellationToken cancellationToken) =>
                TypedResults.Ok(BnplResponse.From(await insights.GetBnplAsync(currentUser.Id, cancellationToken), currentUser.User.Language)))
            .WithName("GetBnpl")
            .WithTags("Insights");

        return api;
    }
}

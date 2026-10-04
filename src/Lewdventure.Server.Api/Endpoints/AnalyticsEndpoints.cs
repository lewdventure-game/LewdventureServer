using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Security;
using Server.Infrastructure.Analytics;
using Server.Infrastructure.Players;

namespace Server.Api.Endpoints
{
    internal sealed class AnalyticsEndpoints
    {
        private const long MaxRequestBodyBytes = 524288;
        private const int MaxReportedErrors = 10;

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

            if (authOptions.Enabled == false)
                return;

            application.MapPost(ApiRoutes.AnalyticsEvents, AcceptAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBodyBytes))
                .Accepts<AnalyticsBatchRequest>("application/json")
                .Produces(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private IResult AcceptAsync(
            HttpContext httpContext,
            [FromBody] AnalyticsBatchRequest? request,
            [FromServices] AnalyticsEventConverter analyticsEventConverter,
            [FromServices] ClientCountryReader clientCountryReader,
            [FromServices] IAnalyticsSink analyticsSink,
            [FromServices] IOptions<AnalyticsOptions> analyticsOptions,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] TimeProvider timeProvider)
        {
            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (request == null || request.Events.Count == 0)
                return Results.BadRequest(new { error = "events are required." });

            if (analyticsOptions.Value.MaxEventsPerRequest < request.Events.Count)
                return Results.BadRequest(new { error = $"At most {analyticsOptions.Value.MaxEventsPerRequest} events per request." });

            var country = clientCountryReader.Read(httpContext.Request);
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var errors = new List<string>();
            var accepted = 0;
            var dropped = 0;

            for (int i = 0; i < request.Events.Count; i++)
            {
                if (analyticsEventConverter.TryConvert(request.Events[i], userId, country, now, out var row, out var error) == false)
                {
                    if (errors.Count < MaxReportedErrors)
                        errors.Add(error);

                    continue;
                }

                if (analyticsSink.TryEnqueue(row!))
                    accepted += 1;
                else
                    dropped += 1;
            }

            return Results.Json(new
            {
                accepted,
                rejected = request.Events.Count - accepted - dropped,
                dropped,
                stored = analyticsSink.IsEnabled,
                errors,
            }, statusCode: StatusCodes.Status202Accepted);
        }
    }
}

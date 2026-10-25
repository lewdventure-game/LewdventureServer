using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class QaPlayerEndpoints
    {
        private const string Source = "qa:self";
        private const int MaxRewardsLength = 1024;

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;
            var cheatOptions = application.Services.GetRequiredService<IOptions<CheatOptions>>().Value;

            if (authOptions.Enabled == false || mongoOptions.Enabled == false || cheatOptions.Enabled == false)
                return;

            application.MapPost(ApiRoutes.QaGrant, GrantAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Accepts<QaGrantRequest>("application/json")
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status409Conflict);
        }

        private async Task<IResult> GrantAsync(
            HttpContext httpContext,
            [FromBody] QaGrantRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerRewardService playerRewardService,
            [FromServices] PlayerResponseFactory playerResponseFactory,
            [FromServices] ILogger<QaPlayerEndpoints> logger)
        {
            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (IsQaAccount(httpContext) == false)
                return Results.Json(new { error = "Only test accounts can grant rewards to themselves. Mark the account as test in the admin panel." }, statusCode: StatusCodes.Status403Forbidden);

            if (request == null || string.IsNullOrWhiteSpace(request.Rewards))
                return Results.BadRequest(new { error = "rewards is required." });

            if (MaxRewardsLength < request.Rewards.Length)
                return Results.BadRequest(new { error = $"rewards is longer than {MaxRewardsLength} characters." });

            var rewards = request.Rewards.Trim();
            var result = await playerRewardService.GrantAsync(userId, rewards, Source, request.RequestId, configDistributor, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            logger.LogInformation("[Qa] self grant userId = {UserId} rewards = {Rewards}", userId, rewards);

            return Results.Ok(playerResponseFactory.Create(result.Profile!));
        }

        private bool IsQaAccount(HttpContext httpContext)
        {
            return httpContext.Items.TryGetValue(PlayerAccountItems.IsQaKey, out var isQa) && isQa is true;
        }
    }
}

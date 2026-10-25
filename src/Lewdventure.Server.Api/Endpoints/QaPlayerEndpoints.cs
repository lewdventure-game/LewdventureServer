using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Runs;
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

            application.MapGet(ApiRoutes.QaRun, GetRunAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .Produces<QaRunResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status404NotFound);

            application.MapPost(ApiRoutes.QaRun, ChangeRunAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .Accepts<CheatRunRequest>("application/json")
                .Produces<QaRunResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status403Forbidden)
                .Produces(StatusCodes.Status409Conflict);
        }

        private async Task<IResult> GetRunAsync(
            HttpContext httpContext,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] QaResponseFactory qaResponseFactory,
            [FromServices] RunCheatService runCheatService,
            [FromServices] RunRepository runRepository)
        {
            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (IsQaAccount(httpContext) == false)
                return Forbidden();

            var run = await runRepository.GetActiveAsync(userId, httpContext.RequestAborted);

            if (run == null)
                return Results.NotFound(new { error = "Player has no active run." });

            var configSet = await runCheatService.ResolveConfigsAsync(run, httpContext.RequestAborted);

            return Results.Ok(qaResponseFactory.CreateRun(run, configSet.Distributor));
        }

        private async Task<IResult> ChangeRunAsync(
            HttpContext httpContext,
            [FromBody] CheatRunRequest? request,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] QaResponseFactory qaResponseFactory,
            [FromServices] RunCheatService runCheatService)
        {
            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (IsQaAccount(httpContext) == false)
                return Forbidden();

            if (request == null || string.IsNullOrWhiteSpace(request.Action))
                return Results.BadRequest(new { error = "action is required." });

            var command = new RunCheatCommand
            {
                Action = request.Action.Trim(),
                Stage = request.Stage,
                Id = request.Id,
                Value = request.Value,
                Count = request.Count,
                Battles = request.Battles,
            };
            var result = await runCheatService.ApplyAsync(userId, command, Source, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Run was changed by another request, try again." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            var configSet = await runCheatService.ResolveConfigsAsync(result.Run!, httpContext.RequestAborted);

            return Results.Ok(qaResponseFactory.CreateRun(result.Run!, configSet.Distributor));
        }

        private IResult Forbidden()
        {
            return Results.Json(new { error = "Only test accounts can use QA cheats. Mark the account as test in the admin panel." }, statusCode: StatusCodes.Status403Forbidden);
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
                return Forbidden();

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

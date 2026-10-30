using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class DialogueEndpoints
    {
        private const string StorageRequiredMessage = "Dialogues require Mongo storage.";
        private const int MaxPicks = 256;

        private bool _isMongoEnabled;

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

            _isMongoEnabled = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value.Enabled;

            if (authOptions.Enabled == false)
                return;

            application.MapPost(ApiRoutes.DialogueComplete, CompleteAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Accepts<DialogueCompleteRequest>("application/json")
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status409Conflict);
        }

        private async Task<IResult> CompleteAsync(
            HttpContext httpContext,
            [FromBody] DialogueCompleteRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerDialogueService playerDialogueService,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (request == null || string.IsNullOrWhiteSpace(request.LaunchKey))
                return Results.BadRequest(new { error = "launchKey is required." });

            if (MaxPicks < request.Picks.Count)
                return Results.BadRequest(new { error = $"picks count is above {MaxPicks}." });

            var completion = new DialogueCompletion(request.LaunchKey, request.Picks);
            var result = await playerDialogueService.CompleteAsync(userId, completion, configDistributor, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(playerResponseFactory.Create(result.Profile!, configDistributor));
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Players;
using Server.Runs;

namespace Server.Api.Endpoints
{
    internal sealed class RunEndpoints
    {
        private const string StorageRequiredMessage = "Runs require Mongo storage.";

        private bool _isMongoEnabled;

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

            _isMongoEnabled = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value.Enabled;

            if (authOptions.Enabled == false)
                return;

            var group = application.MapGroup(ApiRoutes.Run)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata());

            group.MapGet("/current", GetCurrentAsync);
            group.MapPost("/start", StartAsync);
            group.MapPost("/advance", AdvanceAsync);
            group.MapPost("/choose", ChooseAsync);
            group.MapPost("/abandon", AbandonAsync);
        }

        private async Task<IResult> GetCurrentAsync(
            HttpContext httpContext,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] RunResponseFactory runResponseFactory,
            [FromServices] RunService runService)
        {
            if (TryResolveUser(httpContext, playerIdentityReader, out var userId, out var failure) == false)
                return failure;

            var result = await runService.GetCurrentAsync(userId, httpContext.RequestAborted);

            return CreateResponse(result, runResponseFactory, StatusCodes.Status404NotFound);
        }

        private async Task<IResult> StartAsync(
            HttpContext httpContext,
            [FromBody] RunStartRequest? request,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] RunResponseFactory runResponseFactory,
            [FromServices] RunService runService)
        {
            if (TryResolveUser(httpContext, playerIdentityReader, out var userId, out var failure) == false)
                return failure;

            if (request == null || request.StoryLevelId <= 0)
                return Results.BadRequest(new { error = "storyLevelId is required." });

            var result = await runService.StartAsync(userId, request.StoryLevelId, httpContext.RequestAborted);

            return CreateResponse(result, runResponseFactory, StatusCodes.Status400BadRequest);
        }

        private async Task<IResult> AdvanceAsync(
            HttpContext httpContext,
            [FromBody] RunActionRequest? request,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] RunResponseFactory runResponseFactory,
            [FromServices] RunService runService)
        {
            if (TryResolveUser(httpContext, playerIdentityReader, out var userId, out var failure) == false)
                return failure;

            var runId = request == null ? string.Empty : request.RunId;
            var requestId = request == null ? string.Empty : request.RequestId;
            var result = await runService.AdvanceAsync(userId, runId, requestId, httpContext.RequestAborted);

            return CreateResponse(result, runResponseFactory, StatusCodes.Status400BadRequest);
        }

        private async Task<IResult> ChooseAsync(
            HttpContext httpContext,
            [FromBody] RunActionRequest? request,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] RunResponseFactory runResponseFactory,
            [FromServices] RunService runService)
        {
            if (TryResolveUser(httpContext, playerIdentityReader, out var userId, out var failure) == false)
                return failure;

            if (request == null || request.Picks.Count == 0)
                return Results.BadRequest(new { error = "picks are required." });

            var result = await runService.ChooseAsync(userId, request.RunId, request.Picks, request.RequestId, httpContext.RequestAborted);

            return CreateResponse(result, runResponseFactory, StatusCodes.Status400BadRequest);
        }

        private async Task<IResult> AbandonAsync(
            HttpContext httpContext,
            [FromBody] RunActionRequest? request,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] RunResponseFactory runResponseFactory,
            [FromServices] RunService runService)
        {
            if (TryResolveUser(httpContext, playerIdentityReader, out var userId, out var failure) == false)
                return failure;

            var runId = request == null ? string.Empty : request.RunId;
            var result = await runService.AbandonAsync(userId, runId, httpContext.RequestAborted);

            return CreateResponse(result, runResponseFactory, StatusCodes.Status400BadRequest);
        }

        private bool TryResolveUser(HttpContext httpContext, PlayerIdentityReader playerIdentityReader, out string userId, out IResult failure)
        {
            userId = string.Empty;
            failure = Results.Empty;

            if (_isMongoEnabled == false)
            {
                failure = Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

                return false;
            }

            userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId) == false)
                return true;

            failure = Results.Unauthorized();

            return false;
        }

        private IResult CreateResponse(RunOperationResult result, RunResponseFactory runResponseFactory, int errorStatusCode)
        {
            if (result.Conflict)
                return Results.Json(new { error = "Run was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false || result.Run == null)
                return Results.Json(new { errors = result.Errors }, statusCode: errorStatusCode);

            return Results.Ok(runResponseFactory.Create(result.Run, result.Step));
        }
    }
}

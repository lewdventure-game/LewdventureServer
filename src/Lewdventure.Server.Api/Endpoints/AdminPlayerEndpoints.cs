using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class AdminPlayerEndpoints
    {
        private const int MaxLedgerLimit = 200;

        public void Map(WebApplication application)
        {
            var adminOptions = application.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;

            if (adminOptions.Enabled == false || mongoOptions.Enabled == false)
                return;

            var group = application.MapGroup(ApiRoutes.AdminPlayer)
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            group.MapGet("/{userId}", GetProfileAsync);
            group.MapGet("/{userId}/ledger", GetLedgerAsync);
            group.MapGet("/{userId}/export", ExportAsync);
            group.MapPost("/{userId}/grant", GrantAsync).WithMetadata(new GameConfigRequiredMetadata());
            group.MapDelete("/{userId}", DeleteAsync);
        }

        private async Task<IResult> GetProfileAsync(
            HttpContext httpContext,
            string userId,
            [FromServices] PlayerProfileService playerProfileService,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            var profile = await playerProfileService.GetOrCreateAsync(userId, httpContext.RequestAborted);

            return Results.Ok(playerResponseFactory.Create(profile));
        }

        private async Task<IResult> GetLedgerAsync(
            HttpContext httpContext,
            string userId,
            [FromQuery] int? limit,
            [FromServices] PlayerLedgerRepository playerLedgerRepository)
        {
            var effectiveLimit = limit == null || limit.Value <= 0 || MaxLedgerLimit < limit.Value ? 20 : limit.Value;
            var entries = await playerLedgerRepository.ListAsync(userId, effectiveLimit, httpContext.RequestAborted);

            return Results.Ok(entries);
        }

        private async Task<IResult> ExportAsync(
            HttpContext httpContext,
            string userId,
            [FromServices] PlayerDataService playerDataService)
        {
            var export = await playerDataService.ExportAsync(userId, httpContext.RequestAborted);

            return Results.Ok(export);
        }

        private async Task<IResult> DeleteAsync(
            HttpContext httpContext,
            string userId,
            [FromQuery] string? confirm,
            [FromServices] PlayerDataService playerDataService)
        {
            if (string.Equals(confirm, userId, StringComparison.Ordinal) == false)
                return Results.BadRequest(new { error = "confirm must repeat the userId." });

            var actor = httpContext.RequestServices.GetRequiredService<AdminActorReader>().Read(httpContext);
            var deletion = await playerDataService.DeleteAsync(userId, actor, httpContext.RequestAborted);

            if (deletion.HasData == false)
                return Results.NotFound(new { error = "Player has no data." });

            return Results.Ok(deletion);
        }

        private async Task<IResult> GrantAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] PlayerGrantRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerRewardService playerRewardService,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Rewards))
                return Results.BadRequest(new { error = "rewards is required." });

            var actor = httpContext.RequestServices.GetRequiredService<AdminActorReader>().Read(httpContext);
            var source = actor + ":" + (string.IsNullOrWhiteSpace(request.Reason) ? "grant" : request.Reason);
            var result = await playerRewardService.GrantAsync(userId, request.Rewards, source, request.RequestId, configDistributor, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(playerResponseFactory.Create(result.Profile!));
        }
    }
}

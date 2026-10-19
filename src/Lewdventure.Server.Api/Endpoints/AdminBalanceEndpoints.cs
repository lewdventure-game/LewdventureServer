using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.Balance;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Endpoints
{
    internal sealed class AdminBalanceEndpoints
    {
        private const int VersionListLimit = 20;

        private readonly SemaphoreSlim _seriesGate = new(1, 1);

        private int _maxSeriesBattles;

        public void Map(WebApplication application)
        {
            var adminOptions = application.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;
            var balanceOptions = application.Services.GetRequiredService<IOptions<BalanceOptions>>().Value;

            if (adminOptions.Enabled == false || mongoOptions.Enabled == false || balanceOptions.Enabled == false)
                return;

            _maxSeriesBattles = balanceOptions.MaxSeriesBattles;

            var group = application.MapGroup(ApiRoutes.AdminBalance)
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            group.MapGet("/versions", ListVersionsAsync);
            group.MapGet("/catalog", GetCatalogAsync);
            group.MapPost("/characteristics", BuildCharacteristicsAsync);
            group.MapPost("/battle", RunBattleAsync);
            group.MapPost("/series", RunSeriesAsync);
        }

        private async Task<IResult> ListVersionsAsync(
            HttpContext httpContext,
            [FromServices] ConfigPublishingService configPublishingService,
            [FromServices] ConfigSnapshotHasher configSnapshotHasher)
        {
            var activeVersion = await configPublishingService.GetActiveVersionAsync(httpContext.RequestAborted);
            var snapshots = await configPublishingService.ListAsync(VersionListLimit, httpContext.RequestAborted);
            var response = new List<BalanceVersionResponse>(snapshots.Count);

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];

                response.Add(new BalanceVersionResponse
                {
                    Version = snapshot.Version,
                    ShortVersion = configSnapshotHasher.ToShortVersion(snapshot.Version),
                    CreatedAt = snapshot.CreatedAt,
                    CreatedBy = snapshot.CreatedBy,
                    IsActive = string.Equals(snapshot.Version, activeVersion, StringComparison.Ordinal),
                });
            }

            return Results.Ok(response);
        }

        private async Task<IResult> GetCatalogAsync(
            HttpContext httpContext,
            [FromQuery] string? version,
            [FromServices] BalanceCatalogFactory balanceCatalogFactory,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider)
        {
            var configSet = await ResolveAsync(version, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (configSet == null)
                return VersionMissing(version);

            return Results.Ok(balanceCatalogFactory.Create(configSet.Version, configSet.Distributor));
        }

        private async Task<IResult> BuildCharacteristicsAsync(
            HttpContext httpContext,
            [FromBody] BalanceRequest? request,
            [FromServices] BalanceService balanceService,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] PlayerCharacteristicsResponseFactory playerCharacteristicsResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "Body is required." });

            var configSet = await ResolveAsync(request.Version, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (configSet == null)
                return VersionMissing(request.Version);

            if (balanceService.TryBuildCharacteristics(request.Setup, request.Target, configSet.Distributor, out var layers, out var error) == false)
                return Results.BadRequest(new { error });

            var response = new List<BalanceCharacteristicsLayerResponse>(layers.Count);

            for (int i = 0; i < layers.Count; i++)
                response.Add(new BalanceCharacteristicsLayerResponse { Name = layers[i].Name, Characteristics = playerCharacteristicsResponseFactory.Create(layers[i].Characteristics) });

            return Results.Ok(response);
        }

        private async Task<IResult> RunBattleAsync(
            HttpContext httpContext,
            [FromBody] BalanceRequest? request,
            [FromServices] BalanceService balanceService,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider)
        {
            if (request == null)
                return Results.BadRequest(new { error = "Body is required." });

            var configSet = await ResolveAsync(request.Version, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (configSet == null)
                return VersionMissing(request.Version);

            if (balanceService.TryRunBattle(request.Setup, request.Target, request.Seed, request.IncludeLog, configSet.Distributor, out var result, out var error) == false)
                return Results.BadRequest(new { error });

            return Results.Ok(result);
        }

        private async Task<IResult> RunSeriesAsync(
            HttpContext httpContext,
            [FromBody] BalanceRequest? request,
            [FromServices] BalanceService balanceService,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return Results.BadRequest(new { error = "Body is required." });

            if (request.Battles < 1 || _maxSeriesBattles < request.Battles)
                return Results.BadRequest(new { error = $"Battles must be from 1 to {_maxSeriesBattles}." });

            var configSet = await ResolveAsync(request.Version, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (configSet == null)
                return VersionMissing(request.Version);

            var actor = ReadActor(httpContext);

            if (await _seriesGate.WaitAsync(0, httpContext.RequestAborted) == false)
            {
                logger.LogWarning("[Balance] series rejected busy actor = {Actor} battles = {Battles}", actor, request.Battles);

                return Results.Json(new { error = "Another balance series is running, try again in a moment." }, statusCode: StatusCodes.Status409Conflict);
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();

                if (balanceService.TryRunSeries(request.Setup, request.Target, request.Seed, request.Battles, configSet.Distributor, httpContext.RequestAborted, out var series, out var error) == false)
                {
                    logger.LogWarning("[Balance] series rejected actor = {Actor} version = {Version} error = {Error}", actor, configSet.Version, error);

                    return Results.BadRequest(new { error });
                }

                logger.LogInformation(
                    "[Balance] series actor = {Actor} version = {Version} character = {CharacterId} level = {StoryLevelId} stage = {StageId} event = {EventId} battles = {Battles} wins = {Wins} elapsedMs = {ElapsedMs}",
                    actor,
                    configSet.Version,
                    request.Setup.CharacterId,
                    request.Target.StoryLevelId,
                    request.Target.StageId,
                    request.Target.EventId,
                    series.Battles,
                    series.Wins,
                    stopwatch.ElapsedMilliseconds);

                return Results.Ok(series);
            }
            finally
            {
                _seriesGate.Release();
            }
        }

        private string ReadActor(HttpContext httpContext)
        {
            return httpContext.RequestServices.GetRequiredService<AdminActorReader>().Read(httpContext);
        }

        private async Task<GameConfigSet?> ResolveAsync(string? version, GameConfigSetCache gameConfigSetCache, IGameConfigSetProvider gameConfigSetProvider, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(version))
                return gameConfigSetProvider.Current;

            return await gameConfigSetCache.GetAsync(version.Trim(), cancellationToken);
        }

        private IResult VersionMissing(string? version)
        {
            return Results.NotFound(new { error = $"Config version {version} is not published." });
        }
    }
}

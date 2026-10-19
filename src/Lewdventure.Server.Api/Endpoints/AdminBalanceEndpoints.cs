using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.Balance;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Balance;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Endpoints
{
    internal sealed class AdminBalanceEndpoints
    {
        private const int VersionListLimit = 20;
        private const int ScenarioListLimit = 200;
        private const int HistoryDefaultLimit = 50;
        private const int HistoryMaxLimit = 200;
        private const int MaxScenarioNameLength = 80;
        private const int MaxScenarioPayloadLength = 65536;
        private const string BusyError = "Another balance run is in progress, try again in a moment.";

        private readonly SemaphoreSlim _heavyGate = new(1, 1);

        private BalanceOptions _options = new();

        public void Map(WebApplication application)
        {
            var adminOptions = application.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;
            var balanceOptions = application.Services.GetRequiredService<IOptions<BalanceOptions>>().Value;

            if (adminOptions.Enabled == false || mongoOptions.Enabled == false || balanceOptions.Enabled == false)
                return;

            _options = balanceOptions;

            var group = application.MapGroup(ApiRoutes.AdminBalance)
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            group.MapGet("/versions", ListVersionsAsync);
            group.MapGet("/catalog", GetCatalogAsync);
            group.MapPost("/characteristics", BuildCharacteristicsAsync);
            group.MapPost("/battle", RunBattleAsync);
            group.MapPost("/series", RunSeriesAsync);
            group.MapPost("/level", RunLevelAsync);
            group.MapPost("/compare", CompareAsync);
            group.MapPost("/sweep", SweepAsync);
            group.MapGet("/scenarios", ListScenariosAsync);
            group.MapGet("/scenarios/{scenarioId}", GetScenarioAsync);
            group.MapPost("/scenarios", SaveScenarioAsync);
            group.MapDelete("/scenarios/{scenarioId}", DeleteScenarioAsync);
            group.MapGet("/history", ListHistoryAsync);
            group.MapGet("/history/{runId}", GetHistoryAsync);
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
            [FromServices] BalanceConfigDrafts balanceConfigDrafts,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] PlayerCharacteristicsResponseFactory playerCharacteristicsResponseFactory)
        {
            if (request == null)
                return BodyMissing();

            var resolution = await ResolveDraftAsync(request.Version, request.Edits, balanceConfigDrafts, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (resolution.Failure != null)
                return resolution.Failure;

            if (balanceService.TryBuildCharacteristics(request.Setup, request.Target, resolution.Draft!.Distributor!, out var layers, out var error) == false)
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
            [FromServices] BalanceConfigDrafts balanceConfigDrafts,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider)
        {
            if (request == null)
                return BodyMissing();

            var resolution = await ResolveDraftAsync(request.Version, request.Edits, balanceConfigDrafts, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (resolution.Failure != null)
                return resolution.Failure;

            if (balanceService.TryRunBattle(request.Setup, request.Target, request.Seed, request.IncludeLog, resolution.Draft!.Distributor!, out var result, out var error) == false)
                return Results.BadRequest(new { error });

            return Results.Ok(result);
        }

        private async Task<IResult> RunSeriesAsync(
            HttpContext httpContext,
            [FromBody] BalanceRequest? request,
            [FromServices] BalanceService balanceService,
            [FromServices] BalanceConfigDrafts balanceConfigDrafts,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            if (request.Battles < 1 || _options.MaxSeriesBattles < request.Battles)
                return Results.BadRequest(new { error = $"Battles must be from 1 to {_options.MaxSeriesBattles}." });

            if (MaxScenarioPayloadLength < request.Scenario.Length)
                return ScenarioTooLarge();

            var resolution = await ResolveDraftAsync(request.Version, request.Edits, balanceConfigDrafts, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (resolution.Failure != null)
                return resolution.Failure;

            var actor = ReadActor(httpContext);
            var draft = resolution.Draft!;

            if (await _heavyGate.WaitAsync(0, httpContext.RequestAborted) == false)
                return Busy(logger, actor, "series");

            try
            {
                var stopwatch = Stopwatch.StartNew();

                if (balanceService.TryRunSeries(request.Setup, request.Target, request.Seed, request.Battles, draft.Distributor!, httpContext.RequestAborted, out var series, out var error) == false)
                {
                    logger.LogWarning("[Balance] series rejected actor = {Actor} version = {Version} error = {Error}", actor, draft.Version, error);

                    return Results.BadRequest(new { error });
                }

                logger.LogInformation(
                    "[Balance] series actor = {Actor} version = {Version} edits = {Edits} character = {CharacterId} level = {StoryLevelId} stage = {StageId} event = {EventId} battles = {Battles} wins = {Wins} elapsedMs = {ElapsedMs}",
                    actor,
                    draft.Version,
                    request.Edits.Count,
                    request.Setup.CharacterId,
                    request.Target.StoryLevelId,
                    request.Target.StageId,
                    request.Target.EventId,
                    series.Battles,
                    series.Wins,
                    stopwatch.ElapsedMilliseconds);

                await balanceHistoryRecorder.RecordAsync(actor, BalanceSweepRunner.SeriesKind, draft.Version, request.Edits.Count, DescribeSeries(series), request.Scenario, httpContext.RequestAborted);

                return Results.Ok(series);
            }
            finally
            {
                _heavyGate.Release();
            }
        }

        private async Task<IResult> RunLevelAsync(
            HttpContext httpContext,
            [FromBody] BalanceLevelRequest? request,
            [FromServices] BalanceLevelSimulator balanceLevelSimulator,
            [FromServices] BalanceConfigDrafts balanceConfigDrafts,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            if (request.Runs < 1 || _options.MaxLevelRuns < request.Runs)
                return Results.BadRequest(new { error = $"Runs must be from 1 to {_options.MaxLevelRuns}." });

            if (MaxScenarioPayloadLength < request.Scenario.Length)
                return ScenarioTooLarge();

            if (TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out var policy, out var policyError) == false)
                return Results.BadRequest(new { error = policyError });

            var resolution = await ResolveDraftAsync(request.Version, request.Edits, balanceConfigDrafts, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (resolution.Failure != null)
                return resolution.Failure;

            var actor = ReadActor(httpContext);
            var draft = resolution.Draft!;

            if (await _heavyGate.WaitAsync(0, httpContext.RequestAborted) == false)
                return Busy(logger, actor, "level");

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var levelRun = await balanceLevelSimulator.RunAsync(request.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, draft.Version, draft.Distributor!, httpContext.RequestAborted);

                if (levelRun.Result == null)
                {
                    logger.LogWarning("[Balance] level rejected actor = {Actor} version = {Version} error = {Error}", actor, draft.Version, levelRun.Error);

                    return Results.BadRequest(new { error = levelRun.Error });
                }

                logger.LogInformation(
                    "[Balance] level actor = {Actor} version = {Version} edits = {Edits} character = {CharacterId} level = {StoryLevelId} runs = {Runs} completed = {Completed} perkPolicy = {PerkPolicy} forkPolicy = {ForkPolicy} elapsedMs = {ElapsedMs}",
                    actor,
                    draft.Version,
                    request.Edits.Count,
                    request.Setup.CharacterId,
                    request.StoryLevelId,
                    levelRun.Result.Runs,
                    levelRun.Result.Completed,
                    policy.PerkPolicy,
                    policy.ForkPolicy,
                    stopwatch.ElapsedMilliseconds);

                await balanceHistoryRecorder.RecordAsync(actor, BalanceSweepRunner.LevelKind, draft.Version, request.Edits.Count, DescribeLevel(levelRun.Result), request.Scenario, httpContext.RequestAborted);

                return Results.Ok(levelRun.Result);
            }
            finally
            {
                _heavyGate.Release();
            }
        }

        private async Task<IResult> CompareAsync(
            HttpContext httpContext,
            [FromBody] BalanceCompareRequest? request,
            [FromServices] BalanceService balanceService,
            [FromServices] BalanceLevelSimulator balanceLevelSimulator,
            [FromServices] BalanceConfigDrafts balanceConfigDrafts,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            var isLevel = string.Equals(request.Kind, BalanceSweepRunner.LevelKind, StringComparison.OrdinalIgnoreCase);

            if (isLevel == false && string.Equals(request.Kind, BalanceSweepRunner.SeriesKind, StringComparison.OrdinalIgnoreCase) == false)
                return UnknownKind(request.Kind);

            if (isLevel && (request.Runs < 1 || _options.MaxLevelRuns < request.Runs))
                return Results.BadRequest(new { error = $"Runs must be from 1 to {_options.MaxLevelRuns}." });

            if (isLevel == false && (request.Battles < 1 || _options.MaxSeriesBattles < request.Battles))
                return Results.BadRequest(new { error = $"Battles must be from 1 to {_options.MaxSeriesBattles}." });

            if (MaxScenarioPayloadLength < request.Scenario.Length)
                return ScenarioTooLarge();

            if (TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out var policy, out var policyError) == false)
                return Results.BadRequest(new { error = policyError });

            var resolutionA = await ResolveDraftAsync(request.A.Version, request.A.Edits, balanceConfigDrafts, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (resolutionA.Failure != null)
                return resolutionA.Failure;

            var resolutionB = await ResolveDraftAsync(request.B.Version, request.B.Edits, balanceConfigDrafts, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (resolutionB.Failure != null)
                return resolutionB.Failure;

            var actor = ReadActor(httpContext);
            var draftA = resolutionA.Draft!;
            var draftB = resolutionB.Draft!;
            var response = new BalanceCompareResponse { Kind = isLevel ? BalanceSweepRunner.LevelKind : BalanceSweepRunner.SeriesKind, VersionA = draftA.Version, VersionB = draftB.Version };

            if (await _heavyGate.WaitAsync(0, httpContext.RequestAborted) == false)
                return Busy(logger, actor, "compare");

            try
            {
                var stopwatch = Stopwatch.StartNew();
                string summary;

                if (isLevel)
                {
                    var levelA = await balanceLevelSimulator.RunAsync(request.A.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, draftA.Version, draftA.Distributor!, httpContext.RequestAborted);

                    if (levelA.Result == null)
                        return Results.BadRequest(new { error = "A: " + levelA.Error });

                    var levelB = await balanceLevelSimulator.RunAsync(request.B.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, draftB.Version, draftB.Distributor!, httpContext.RequestAborted);

                    if (levelB.Result == null)
                        return Results.BadRequest(new { error = "B: " + levelB.Error });

                    response.LevelA = levelA.Result;
                    response.LevelB = levelB.Result;
                    summary = $"сравнение уровня {request.StoryLevelId} x{request.Runs}: A {Percent(levelA.Result.CompletionRate)} / B {Percent(levelB.Result.CompletionRate)}";
                }
                else
                {
                    if (balanceService.TryRunSeries(request.A.Setup, request.Target, (ulong)request.Seed, request.Battles, draftA.Distributor!, httpContext.RequestAborted, out var seriesA, out var errorA) == false)
                        return Results.BadRequest(new { error = "A: " + errorA });

                    if (balanceService.TryRunSeries(request.B.Setup, request.Target, (ulong)request.Seed, request.Battles, draftB.Distributor!, httpContext.RequestAborted, out var seriesB, out var errorB) == false)
                        return Results.BadRequest(new { error = "B: " + errorB });

                    response.SeriesA = seriesA;
                    response.SeriesB = seriesB;
                    summary = $"сравнение серий x{request.Battles}: A {Percent(seriesA.WinRate)} / B {Percent(seriesB.WinRate)}";
                }

                logger.LogInformation(
                    "[Balance] compare actor = {Actor} kind = {Kind} versionA = {VersionA} versionB = {VersionB} editsA = {EditsA} editsB = {EditsB} elapsedMs = {ElapsedMs}",
                    actor,
                    response.Kind,
                    draftA.Version,
                    draftB.Version,
                    request.A.Edits.Count,
                    request.B.Edits.Count,
                    stopwatch.ElapsedMilliseconds);

                await balanceHistoryRecorder.RecordAsync(actor, "compare", draftA.Version + " | " + draftB.Version, request.A.Edits.Count + request.B.Edits.Count, summary, request.Scenario, httpContext.RequestAborted);

                return Results.Ok(response);
            }
            finally
            {
                _heavyGate.Release();
            }
        }

        private async Task<IResult> SweepAsync(
            HttpContext httpContext,
            [FromBody] BalanceSweepRequest? request,
            [FromServices] BalanceSweepRunner balanceSweepRunner,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] GameConfigSetCache gameConfigSetCache,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            var isLevel = string.Equals(request.Kind, BalanceSweepRunner.LevelKind, StringComparison.OrdinalIgnoreCase);

            if (isLevel == false && string.Equals(request.Kind, BalanceSweepRunner.SeriesKind, StringComparison.OrdinalIgnoreCase) == false)
                return UnknownKind(request.Kind);

            if (MaxScenarioPayloadLength < request.Scenario.Length)
                return ScenarioTooLarge();

            if (balanceSweepRunner.TryCreateValues(request.Axis, _options.MaxSweepPoints, out var values, out var axisError) == false)
                return Results.BadRequest(new { error = axisError });

            if (isLevel && (request.Runs < 1 || _options.MaxSweepRuns < request.Runs * values.Count))
                return Results.BadRequest(new { error = $"Sweep runs ({values.Count} points x {request.Runs}) must be from 1 to {_options.MaxSweepRuns}." });

            if (isLevel == false && (request.Battles < 1 || _options.MaxSweepBattles < request.Battles * values.Count))
                return Results.BadRequest(new { error = $"Sweep battles ({values.Count} points x {request.Battles}) must be from 1 to {_options.MaxSweepBattles}." });

            if (TryReadPolicy(request.PerkPolicy, request.PerkPriority, request.ForkPolicy, out var policy, out var policyError) == false)
                return Results.BadRequest(new { error = policyError });

            var configSet = await ResolveAsync(request.Version, gameConfigSetCache, gameConfigSetProvider, httpContext.RequestAborted);

            if (configSet == null)
                return VersionMissing(request.Version);

            var actor = ReadActor(httpContext);

            if (await _heavyGate.WaitAsync(0, httpContext.RequestAborted) == false)
                return Busy(logger, actor, "sweep");

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = isLevel
                    ? await balanceSweepRunner.RunLevelAsync(configSet, request.Edits, request.Axis, values, request.Setup, request.StoryLevelId, request.Seed, request.Runs, policy, httpContext.RequestAborted)
                    : balanceSweepRunner.RunSeries(configSet, request.Edits, request.Axis, values, request.Setup, request.Target, (ulong)request.Seed, request.Battles, httpContext.RequestAborted);

                logger.LogInformation(
                    "[Balance] sweep actor = {Actor} kind = {Kind} version = {Version} edits = {Edits} sheet = {Sheet} row = {Row} column = {Column} points = {Points} elapsedMs = {ElapsedMs}",
                    actor,
                    result.Kind,
                    configSet.Version,
                    request.Edits.Count,
                    request.Axis.Sheet,
                    request.Axis.Row,
                    request.Axis.Column,
                    result.Points.Count,
                    stopwatch.ElapsedMilliseconds);

                await balanceHistoryRecorder.RecordAsync(actor, "sweep", configSet.Version, request.Edits.Count, DescribeSweep(result), request.Scenario, httpContext.RequestAborted);

                return Results.Ok(result);
            }
            finally
            {
                _heavyGate.Release();
            }
        }

        private async Task<IResult> ListScenariosAsync(HttpContext httpContext, [FromServices] BalanceScenarioRepository balanceScenarioRepository)
        {
            var documents = await balanceScenarioRepository.ListAsync(ScenarioListLimit, httpContext.RequestAborted);
            var response = new List<BalanceScenarioResponse>(documents.Count);

            for (int i = 0; i < documents.Count; i++)
                response.Add(ToScenarioResponse(documents[i], false));

            return Results.Ok(response);
        }

        private async Task<IResult> GetScenarioAsync(HttpContext httpContext, string scenarioId, [FromServices] BalanceScenarioRepository balanceScenarioRepository)
        {
            var document = await balanceScenarioRepository.GetAsync(scenarioId, httpContext.RequestAborted);

            if (document == null)
                return Results.NotFound(new { error = $"Scenario {scenarioId} is not found." });

            return Results.Ok(ToScenarioResponse(document, true));
        }

        private async Task<IResult> SaveScenarioAsync(
            HttpContext httpContext,
            [FromBody] BalanceScenarioRequest? request,
            [FromServices] BalanceScenarioRepository balanceScenarioRepository,
            [FromServices] TimeProvider timeProvider,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            var name = (request.Name ?? string.Empty).Trim();

            if (name.Length == 0 || MaxScenarioNameLength < name.Length)
                return Results.BadRequest(new { error = $"Scenario name must be from 1 to {MaxScenarioNameLength} characters." });

            if (string.IsNullOrEmpty(request.Payload) || MaxScenarioPayloadLength < request.Payload.Length)
                return Results.BadRequest(new { error = $"Scenario payload must be from 1 to {MaxScenarioPayloadLength} characters." });

            var actor = ReadActor(httpContext);
            var scenarioId = name.ToLowerInvariant();
            var now = timeProvider.GetUtcNow().UtcDateTime;
            var existing = await balanceScenarioRepository.GetAsync(scenarioId, httpContext.RequestAborted);
            var document = new BalanceScenarioDocument
            {
                Id = scenarioId,
                Name = name,
                CreatedAt = existing == null ? now : existing.CreatedAt,
                CreatedBy = existing == null ? actor : existing.CreatedBy,
                UpdatedAt = now,
                UpdatedBy = actor,
                Payload = request.Payload,
            };

            await balanceScenarioRepository.UpsertAsync(document, httpContext.RequestAborted);
            logger.LogInformation("[Balance] scenario saved scenario = {ScenarioId} actor = {Actor} overwritten = {Overwritten}", scenarioId, actor, existing != null);

            return Results.Ok(ToScenarioResponse(document, false));
        }

        private async Task<IResult> DeleteScenarioAsync(
            HttpContext httpContext,
            string scenarioId,
            [FromServices] BalanceScenarioRepository balanceScenarioRepository,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (await balanceScenarioRepository.DeleteAsync(scenarioId, httpContext.RequestAborted) == 0)
                return Results.NotFound(new { error = $"Scenario {scenarioId} is not found." });

            logger.LogInformation("[Balance] scenario deleted scenario = {ScenarioId} actor = {Actor}", scenarioId, ReadActor(httpContext));

            return Results.NoContent();
        }

        private async Task<IResult> ListHistoryAsync(HttpContext httpContext, [FromQuery] int? limit, [FromServices] BalanceRunRepository balanceRunRepository)
        {
            var documents = await balanceRunRepository.ListAsync(Math.Clamp(limit ?? HistoryDefaultLimit, 1, HistoryMaxLimit), httpContext.RequestAborted);
            var response = new List<BalanceRunResponse>(documents.Count);

            for (int i = 0; i < documents.Count; i++)
                response.Add(ToRunResponse(documents[i], false));

            return Results.Ok(response);
        }

        private async Task<IResult> GetHistoryAsync(HttpContext httpContext, string runId, [FromServices] BalanceRunRepository balanceRunRepository)
        {
            var document = await balanceRunRepository.GetAsync(runId, httpContext.RequestAborted);

            if (document == null)
                return Results.NotFound(new { error = $"Balance run {runId} is not found." });

            return Results.Ok(ToRunResponse(document, true));
        }

        private BalanceScenarioResponse ToScenarioResponse(BalanceScenarioDocument document, bool includePayload)
        {
            return new BalanceScenarioResponse
            {
                Id = document.Id,
                Name = document.Name,
                CreatedBy = document.CreatedBy,
                UpdatedBy = document.UpdatedBy,
                CreatedAt = document.CreatedAt,
                UpdatedAt = document.UpdatedAt,
                Payload = includePayload ? document.Payload : string.Empty,
            };
        }

        private BalanceRunResponse ToRunResponse(BalanceRunDocument document, bool includePayload)
        {
            return new BalanceRunResponse
            {
                Id = document.Id,
                CreatedAt = document.CreatedAt,
                Actor = document.Actor,
                Kind = document.Kind,
                ConfigVersion = document.ConfigVersion,
                Edits = document.Edits,
                Summary = document.Summary,
                Payload = includePayload ? document.Payload : string.Empty,
            };
        }

        private bool TryReadPolicy(string perkPolicyText, List<int> perkPriority, string forkPolicyText, out BalanceLevelPolicy policy, out string error)
        {
            policy = new BalanceLevelPolicy { PerkPriority = new List<int>(perkPriority) };
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(perkPolicyText) == false)
            {
                if (Enum.TryParse<BalancePerkPolicy>(perkPolicyText, true, out var perkPolicy) == false)
                {
                    error = $"Unknown perk policy {perkPolicyText}, use first, random or priority.";

                    return false;
                }

                policy.PerkPolicy = perkPolicy;
            }

            if (string.IsNullOrWhiteSpace(forkPolicyText) == false)
            {
                if (Enum.TryParse<BalanceForkPolicy>(forkPolicyText, true, out var forkPolicy) == false)
                {
                    error = $"Unknown fork policy {forkPolicyText}, use firstButton, secondButton or random.";

                    return false;
                }

                policy.ForkPolicy = forkPolicy;
            }

            return true;
        }

        private string DescribeSeries(BalanceSeriesResult series)
        {
            return $"серия x{series.Battles}: победы {series.Wins} ({Percent(series.WinRate)})";
        }

        private string DescribeLevel(BalanceLevelResult level)
        {
            return $"уровень {level.StoryLevelId} x{level.Runs}: прошли {level.Completed} ({Percent(level.CompletionRate)}), этапов в среднем {level.StagesCleared.Average.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        private string DescribeSweep(BalanceSweepResult result)
        {
            var axis = result.Axis;

            return $"перебор {result.Kind} {axis.Sheet} / {axis.Row} / {axis.Column} от {axis.From.ToString(CultureInfo.InvariantCulture)} до {axis.To.ToString(CultureInfo.InvariantCulture)}: {result.Points.Count} точек";
        }

        private string Percent(float value)
        {
            return (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }

        private string ReadActor(HttpContext httpContext)
        {
            return httpContext.RequestServices.GetRequiredService<AdminActorReader>().Read(httpContext);
        }

        private async Task<BalanceDraftResolution> ResolveDraftAsync(
            string? version,
            List<BalanceConfigEdit> edits,
            BalanceConfigDrafts balanceConfigDrafts,
            GameConfigSetCache gameConfigSetCache,
            IGameConfigSetProvider gameConfigSetProvider,
            CancellationToken cancellationToken)
        {
            var configSet = await ResolveAsync(version, gameConfigSetCache, gameConfigSetProvider, cancellationToken);

            if (configSet == null)
                return new BalanceDraftResolution(null, null, VersionMissing(version));

            var draft = balanceConfigDrafts.Apply(configSet, edits ?? new List<BalanceConfigEdit>());

            if (draft.Distributor == null)
                return new BalanceDraftResolution(configSet, draft, Results.BadRequest(new { error = "Draft edits are invalid: " + string.Join("; ", draft.Errors) }));

            return new BalanceDraftResolution(configSet, draft, null);
        }

        private async Task<GameConfigSet?> ResolveAsync(string? version, GameConfigSetCache gameConfigSetCache, IGameConfigSetProvider gameConfigSetProvider, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(version))
                return gameConfigSetProvider.Current;

            return await gameConfigSetCache.GetAsync(version.Trim(), cancellationToken);
        }

        private IResult Busy(ILogger<AdminBalanceEndpoints> logger, string actor, string kind)
        {
            logger.LogWarning("[Balance] {Kind} rejected busy actor = {Actor}", kind, actor);

            return Results.Json(new { error = BusyError }, statusCode: StatusCodes.Status409Conflict);
        }

        private IResult BodyMissing()
        {
            return Results.BadRequest(new { error = "Body is required." });
        }

        private IResult ScenarioTooLarge()
        {
            return Results.BadRequest(new { error = $"Scenario must be at most {MaxScenarioPayloadLength} characters." });
        }

        private IResult UnknownKind(string kind)
        {
            return Results.BadRequest(new { error = $"Unknown kind {kind}, use series or level." });
        }

        private IResult VersionMissing(string? version)
        {
            return Results.NotFound(new { error = $"Config version {version} is not published." });
        }
    }
}

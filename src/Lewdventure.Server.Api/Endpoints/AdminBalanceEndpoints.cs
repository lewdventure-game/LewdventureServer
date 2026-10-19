using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Server.Api.Balance;
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
            group.MapGet("/progress", FindProgressAsync);
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
            group.MapPost("/jobs", CreateJobAsync);
            group.MapGet("/jobs/{jobId}", GetJobAsync);
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

        private async Task<IResult> FindProgressAsync(
            HttpContext httpContext,
            [FromQuery] int? storyLevelId,
            [FromQuery] string? version,
            [FromServices] BalanceProgressService balanceProgressService,
            [FromServices] BalanceRunExecutor balanceRunExecutor)
        {
            var configSet = await balanceRunExecutor.ResolveAsync(version, httpContext.RequestAborted);

            if (configSet == null)
                return VersionMissing(version);

            var levelId = storyLevelId ?? 0;

            if (configSet.Distributor.StoryLevels.TryGet(levelId, out _) == false)
                return Results.BadRequest(new { error = $"Story level {levelId} is missing in configs." });

            return Results.Ok(await balanceProgressService.FindTypicalAsync(levelId, configSet.Distributor, httpContext.RequestAborted));
        }

        private async Task<IResult> BuildCharacteristicsAsync(
            HttpContext httpContext,
            [FromBody] BalanceRequest? request,
            [FromServices] BalanceService balanceService,
            [FromServices] BalanceRunExecutor balanceRunExecutor,
            [FromServices] PlayerCharacteristicsResponseFactory playerCharacteristicsResponseFactory)
        {
            if (request == null)
                return BodyMissing();

            var resolution = await balanceRunExecutor.ResolveDraftAsync(request.Version, request.Edits, httpContext.RequestAborted);

            if (resolution.Draft == null)
                return Failure(resolution.Failure!);

            if (balanceService.TryBuildCharacteristics(request.Setup, request.Target, resolution.Draft.Distributor!, out var layers, out var error) == false)
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
            [FromServices] BalanceRunExecutor balanceRunExecutor)
        {
            if (request == null)
                return BodyMissing();

            var resolution = await balanceRunExecutor.ResolveDraftAsync(request.Version, request.Edits, httpContext.RequestAborted);

            if (resolution.Draft == null)
                return Failure(resolution.Failure!);

            if (balanceService.TryRunBattle(request.Setup, request.Target, request.Seed, request.IncludeLog, resolution.Draft.Distributor!, out var result, out var error) == false)
                return Results.BadRequest(new { error });

            return Results.Ok(result);
        }

        private async Task<IResult> RunSeriesAsync(
            HttpContext httpContext,
            [FromBody] BalanceRequest? request,
            [FromServices] BalanceRunExecutor balanceRunExecutor,
            [FromServices] BalanceRunGate balanceRunGate,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            return await RunHeavyAsync(httpContext, BalanceRunKinds.Series, balanceRunExecutor.ValidateSeries(request), (progress, token) => balanceRunExecutor.RunSeriesAsync(request, progress, token), balanceRunGate, balanceHistoryRecorder, logger);
        }

        private async Task<IResult> RunLevelAsync(
            HttpContext httpContext,
            [FromBody] BalanceLevelRequest? request,
            [FromServices] BalanceRunExecutor balanceRunExecutor,
            [FromServices] BalanceRunGate balanceRunGate,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            return await RunHeavyAsync(httpContext, BalanceRunKinds.Level, balanceRunExecutor.ValidateLevel(request), (progress, token) => balanceRunExecutor.RunLevelAsync(request, progress, token), balanceRunGate, balanceHistoryRecorder, logger);
        }

        private async Task<IResult> CompareAsync(
            HttpContext httpContext,
            [FromBody] BalanceCompareRequest? request,
            [FromServices] BalanceRunExecutor balanceRunExecutor,
            [FromServices] BalanceRunGate balanceRunGate,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            return await RunHeavyAsync(httpContext, BalanceRunKinds.Compare, balanceRunExecutor.ValidateCompare(request), (progress, token) => balanceRunExecutor.RunCompareAsync(request, progress, token), balanceRunGate, balanceHistoryRecorder, logger);
        }

        private async Task<IResult> SweepAsync(
            HttpContext httpContext,
            [FromBody] BalanceSweepRequest? request,
            [FromServices] BalanceRunExecutor balanceRunExecutor,
            [FromServices] BalanceRunGate balanceRunGate,
            [FromServices] BalanceHistoryRecorder balanceHistoryRecorder,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            return await RunHeavyAsync(httpContext, BalanceRunKinds.Sweep, balanceRunExecutor.ValidateSweep(request), (progress, token) => balanceRunExecutor.RunSweepAsync(request, progress, token), balanceRunGate, balanceHistoryRecorder, logger);
        }

        private async Task<IResult> RunHeavyAsync(
            HttpContext httpContext,
            string kind,
            string validationError,
            Func<IBalanceProgress, CancellationToken, Task<BalanceExecution>> run,
            BalanceRunGate balanceRunGate,
            BalanceHistoryRecorder balanceHistoryRecorder,
            ILogger<AdminBalanceEndpoints> logger)
        {
            if (validationError.Length != 0)
                return Results.BadRequest(new { error = validationError });

            var actor = ReadActor(httpContext);

            if (await balanceRunGate.TryEnterAsync(httpContext.RequestAborted) == false)
            {
                logger.LogWarning("[Balance] {Kind} rejected busy actor = {Actor}", kind, actor);

                return Results.Json(new { error = BusyError }, statusCode: StatusCodes.Status409Conflict);
            }

            try
            {
                var stopwatch = Stopwatch.StartNew();
                var execution = await run(new BalanceSilentProgress(), httpContext.RequestAborted);

                if (execution.Result == null)
                {
                    logger.LogWarning("[Balance] {Kind} rejected actor = {Actor} error = {Error}", kind, actor, execution.Error);

                    return Results.Json(new { error = execution.Error }, statusCode: execution.StatusCode);
                }

                execution.Result.RunId = await balanceHistoryRecorder.RecordAsync(actor, kind, execution.ConfigVersion, execution.Edits, execution.Summary, execution.Scenario, httpContext.RequestAborted);
                logger.LogInformation(
                    "[Balance] {Kind} actor = {Actor} version = {Version} edits = {Edits} summary = {Summary} elapsedMs = {ElapsedMs}",
                    kind,
                    actor,
                    execution.ConfigVersion,
                    execution.Edits,
                    execution.Summary,
                    stopwatch.ElapsedMilliseconds);

                return Results.Ok((object)execution.Result);
            }
            finally
            {
                balanceRunGate.Exit();
            }
        }

        private async Task<IResult> CreateJobAsync(
            HttpContext httpContext,
            [FromBody] BalanceJobRequest? request,
            [FromServices] BalanceJobService balanceJobService,
            [FromServices] ILogger<AdminBalanceEndpoints> logger)
        {
            if (request == null)
                return BodyMissing();

            if (_options.WorkerEnabled == false)
                return Results.Json(new { error = "Balance worker is disabled, use the direct endpoints." }, statusCode: StatusCodes.Status409Conflict);

            var actor = ReadActor(httpContext);
            var creation = await balanceJobService.CreateAsync(request.Kind, request.Request, actor, httpContext.RequestAborted);

            if (creation.JobId.Length == 0)
            {
                logger.LogWarning("[Balance] job rejected kind = {Kind} actor = {Actor} error = {Error}", request.Kind, actor, creation.Error);

                return Results.Json(new { error = creation.Error }, statusCode: creation.StatusCode);
            }

            logger.LogInformation("[Balance] job queued job = {JobId} kind = {Kind} actor = {Actor}", creation.JobId, request.Kind, actor);

            return Results.Accepted(ApiRoutes.AdminBalance + "/jobs/" + creation.JobId, new { id = creation.JobId, status = BalanceJobDocument.QueuedStatus });
        }

        private async Task<IResult> GetJobAsync(HttpContext httpContext, string jobId, [FromServices] BalanceJobRepository balanceJobRepository)
        {
            var document = await balanceJobRepository.GetAsync(jobId, httpContext.RequestAborted);

            if (document == null)
                return Results.NotFound(new { error = $"Balance job {jobId} is not found." });

            var response = new BalanceJobResponse
            {
                Id = document.Id,
                Kind = document.Kind,
                Status = document.Status,
                Actor = document.Actor,
                Done = document.Done,
                Total = document.Total,
                Error = document.Error,
                StatusCode = document.StatusCode,
                RunId = document.RunId,
                CreatedAt = document.CreatedAt,
                StartedAt = document.StartedAt,
                FinishedAt = document.FinishedAt,
                Request = document.Request,
            };

            if (document.Result.Length != 0)
            {
                using var json = JsonDocument.Parse(document.Result);

                response.Result = json.RootElement.Clone();
            }

            return Results.Ok(response);
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

        private IResult Failure(BalanceExecution execution)
        {
            return Results.Json(new { error = execution.Error }, statusCode: execution.StatusCode);
        }

        private IResult BodyMissing()
        {
            return Results.BadRequest(new { error = "Body is required." });
        }

        private IResult VersionMissing(string? version)
        {
            return Results.NotFound(new { error = $"Config version {version} is not published." });
        }
    }
}

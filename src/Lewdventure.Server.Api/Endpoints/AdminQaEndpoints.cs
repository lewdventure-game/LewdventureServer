using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Options;
using Server.Api.Security;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;
using Server.Infrastructure.Players;
using Server.Infrastructure.Qa;
using Server.Runs;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class AdminQaEndpoints
    {
        private bool _isCheatsEnabled;

        public void Map(WebApplication application)
        {
            var adminOptions = application.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;

            if (adminOptions.Enabled == false || mongoOptions.Enabled == false)
                return;

            _isCheatsEnabled = application.Services.GetRequiredService<IOptions<CheatOptions>>().Value.Enabled;

            var group = application.MapGroup(ApiRoutes.AdminQa)
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            group.MapGet("/status", GetStatus);
            group.MapGet("/catalog", GetCatalog).WithMetadata(new GameConfigRequiredMetadata());
            group.MapGet("/players", ListAsync);
            group.MapGet("/find", FindAsync);
            group.MapGet("/players/{userId}", GetAccountAsync);
            group.MapPost("/players/{userId}/mark", MarkAsync);
            group.MapDelete("/players/{userId}/mark", UnmarkAsync);

            if (_isCheatsEnabled == false)
                return;

            group.MapGet("/templates", ListTemplatesAsync);
            group.MapPost("/templates", SaveTemplateAsync);
            group.MapDelete("/templates/{templateId}", DeleteTemplateAsync);
            group.MapGet("/players/{userId}/run", GetRunAsync);

            var cheats = group.MapGroup("/players/{userId}/cheats");

            cheats.MapPost("/preset", GrantPresetAsync).WithMetadata(new GameConfigRequiredMetadata());
            cheats.MapPost("/resource", SetResourceAsync);
            cheats.MapPost("/flag", SetFlagAsync);
            cheats.MapPost("/character/{characterId:int}", SetCharacterAsync).WithMetadata(new GameConfigRequiredMetadata());
            cheats.MapPost("/summon/{summonId:int}", SetSummonAsync).WithMetadata(new GameConfigRequiredMetadata());
            cheats.MapPost("/equipment/{instanceId}", SetEquipmentAsync).WithMetadata(new GameConfigRequiredMetadata());
            cheats.MapPost("/max", MaxOutAsync).WithMetadata(new GameConfigRequiredMetadata());
            cheats.MapPost("/story", SetStoryAsync).WithMetadata(new GameConfigRequiredMetadata());
            cheats.MapPost("/run", ChangeRunAsync);
            cheats.MapPost("/template", ApplyTemplateAsync);
            cheats.MapPost("/copy", CopyProfileAsync);
            cheats.MapPost("/reset", ResetAsync);
            cheats.MapPost("/run/abandon", AbandonRunAsync);
        }

        private IResult GetStatus([FromServices] IHostEnvironment hostEnvironment)
        {
            return Results.Ok(new QaStatusResponse
            {
                CheatsEnabled = _isCheatsEnabled,
                Environment = hostEnvironment.EnvironmentName,
            });
        }

        private IResult GetCatalog(
            [FromServices] CheatCatalogFactory cheatCatalogFactory,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider)
        {
            return Results.Ok(cheatCatalogFactory.Create(gameConfigSetProvider.Current.Version, configDistributor));
        }

        private async Task<IResult> ListAsync(HttpContext httpContext, [FromServices] QaAccountService qaAccountService)
        {
            var users = await qaAccountService.ListAsync(httpContext.RequestAborted);
            var response = new List<QaAccountResponse>(users.Count);

            for (int i = 0; i < users.Count; i++)
                response.Add(CreateAccount(users[i]));

            return Results.Ok(response);
        }

        private async Task<IResult> FindAsync(HttpContext httpContext, [FromQuery] string? query, [FromServices] QaAccountService qaAccountService)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Results.BadRequest(new { error = "query is required." });

            var matches = await qaAccountService.FindAsync(query, httpContext.RequestAborted);

            return Results.Ok(matches);
        }

        private async Task<IResult> GetAccountAsync(HttpContext httpContext, string userId, [FromServices] UserRepository userRepository)
        {
            var user = await userRepository.GetAsync(userId, httpContext.RequestAborted);

            if (user == null)
                return Results.NotFound(new { error = $"User {userId} is not found." });

            return Results.Ok(CreateAccount(user));
        }

        private async Task<IResult> MarkAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] QaMarkRequest? request,
            [FromServices] QaAccountService qaAccountService)
        {
            var actor = ReadActor(httpContext);
            var result = await qaAccountService.MarkAsync(userId, request == null ? string.Empty : request.Alias, actor, httpContext.RequestAborted);

            if (result.NotFound)
                return Results.NotFound(new { error = $"User {userId} is not found." });

            if (result.Succeeded == false)
                return Results.BadRequest(new { error = result.Error });

            return Results.Ok(new { userId });
        }

        private async Task<IResult> UnmarkAsync(HttpContext httpContext, string userId, [FromServices] QaAccountService qaAccountService)
        {
            if (await qaAccountService.UnmarkAsync(userId, ReadActor(httpContext), httpContext.RequestAborted) == false)
                return Results.NotFound(new { error = $"User {userId} is not found." });

            return Results.Ok(new { userId });
        }

        private async Task<IResult> GrantPresetAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatPresetRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "preset is required." });

            var result = await cheatService.GrantPresetAsync(userId, request.Preset, request.Amount, ReadActor(httpContext), configDistributor, httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> SetResourceAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatResourceRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "key and amount are required." });

            var result = await cheatService.SetResourceAsync(userId, request.Key.Trim(), request.Amount, ReadActor(httpContext), httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> SetFlagAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatFlagRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "key and value are required." });

            var result = await cheatService.SetFlagAsync(userId, request.Key.Trim(), request.Value, ReadActor(httpContext), httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> SetCharacterAsync(
            HttpContext httpContext,
            string userId,
            int characterId,
            [FromBody] CheatCharacterRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "promoteLevel is required." });

            var result = await cheatService.SetCharacterAsync(userId, characterId, request.PromoteLevel, ReadActor(httpContext), configDistributor, httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> SetSummonAsync(
            HttpContext httpContext,
            string userId,
            int summonId,
            [FromBody] CheatSummonRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "level, masteryLevel or skillLevel is required." });

            var result = await cheatService.SetSummonAsync(
                userId,
                summonId,
                request.Level,
                request.MasteryLevel,
                request.SkillLevel,
                ReadActor(httpContext),
                configDistributor,
                httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> SetEquipmentAsync(
            HttpContext httpContext,
            string userId,
            string instanceId,
            [FromBody] CheatEquipmentRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (request == null)
                return Results.BadRequest(new { error = "level is required." });

            var result = await cheatService.SetEquipmentAsync(userId, instanceId, request.Level, ReadActor(httpContext), configDistributor, httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> MaxOutAsync(
            HttpContext httpContext,
            string userId,
            [FromServices] CheatService cheatService,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            var result = await cheatService.MaxOutAsync(userId, ReadActor(httpContext), configDistributor, httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> SetStoryAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatStoryRequest? request,
            [FromServices] CheatService cheatService,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            var levelIds = request == null ? new List<int>() : request.CompletedLevelIds;
            var result = await cheatService.SetCompletedLevelsAsync(userId, levelIds, ReadActor(httpContext), configDistributor, httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> GetRunAsync(
            HttpContext httpContext,
            string userId,
            [FromServices] QaResponseFactory qaResponseFactory,
            [FromServices] RunCheatService runCheatService,
            [FromServices] RunRepository runRepository)
        {
            var run = await runRepository.GetActiveAsync(userId, httpContext.RequestAborted);

            if (run == null)
                return Results.NotFound(new { error = "Player has no active run." });

            var configSet = await runCheatService.ResolveConfigsAsync(run, httpContext.RequestAborted);

            return Results.Ok(qaResponseFactory.CreateRun(run, configSet.Distributor));
        }

        private async Task<IResult> ChangeRunAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatRunRequest? request,
            [FromServices] QaResponseFactory qaResponseFactory,
            [FromServices] RunCheatService runCheatService)
        {
            if (request == null)
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
            var result = await runCheatService.ApplyAsync(userId, command, ReadActor(httpContext), httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Run was changed by another request, try again." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            var configSet = await runCheatService.ResolveConfigsAsync(result.Run!, httpContext.RequestAborted);

            return Results.Ok(qaResponseFactory.CreateRun(result.Run!, configSet.Distributor));
        }

        private async Task<IResult> ListTemplatesAsync(
            HttpContext httpContext,
            [FromServices] QaResponseFactory qaResponseFactory,
            [FromServices] QaTemplateService qaTemplateService)
        {
            var templates = await qaTemplateService.ListAsync(httpContext.RequestAborted);
            var response = new List<QaTemplateResponse>(templates.Count);

            for (int i = 0; i < templates.Count; i++)
                response.Add(qaResponseFactory.CreateTemplate(templates[i]));

            return Results.Ok(response);
        }

        private async Task<IResult> SaveTemplateAsync(
            HttpContext httpContext,
            [FromBody] QaTemplateSaveRequest? request,
            [FromServices] IGameConfigSetProvider gameConfigSetProvider,
            [FromServices] QaTemplateService qaTemplateService)
        {
            if (request == null)
                return Results.BadRequest(new { error = "templateId, name and userId are required." });

            var error = await qaTemplateService.SaveAsync(
                request.UserId.Trim(),
                request.TemplateId,
                request.Name,
                request.Description,
                gameConfigSetProvider.Current.Version,
                ReadActor(httpContext),
                httpContext.RequestAborted);

            if (error.Length != 0)
                return Results.BadRequest(new { error });

            return Results.Ok(new { templateId = request.TemplateId });
        }

        private async Task<IResult> DeleteTemplateAsync(HttpContext httpContext, string templateId, [FromServices] QaTemplateService qaTemplateService)
        {
            if (await qaTemplateService.DeleteAsync(templateId, ReadActor(httpContext), httpContext.RequestAborted) == false)
                return Results.NotFound(new { error = $"Template {templateId} is not found." });

            return Results.Ok(new { templateId });
        }

        private async Task<IResult> ApplyTemplateAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatTemplateApplyRequest? request,
            [FromServices] PlayerResponseFactory playerResponseFactory,
            [FromServices] QaTemplateService qaTemplateService,
            [FromServices] RunService runService)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TemplateId))
                return Results.BadRequest(new { error = "templateId is required." });

            var abandonError = await AbandonActiveRunAsync(userId, runService, httpContext.RequestAborted);

            if (abandonError.Length != 0)
                return Results.BadRequest(new { error = abandonError });

            var result = await qaTemplateService.ApplyTemplateAsync(request.TemplateId.Trim(), userId, ReadActor(httpContext), httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<IResult> CopyProfileAsync(
            HttpContext httpContext,
            string userId,
            [FromBody] CheatCopyRequest? request,
            [FromServices] PlayerResponseFactory playerResponseFactory,
            [FromServices] QaTemplateService qaTemplateService,
            [FromServices] RunService runService)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.SourceUserId))
                return Results.BadRequest(new { error = "sourceUserId is required." });

            var abandonError = await AbandonActiveRunAsync(userId, runService, httpContext.RequestAborted);

            if (abandonError.Length != 0)
                return Results.BadRequest(new { error = abandonError });

            var result = await qaTemplateService.CopyProfileAsync(request.SourceUserId.Trim(), userId, ReadActor(httpContext), httpContext.RequestAborted);

            return ToResult(result, playerResponseFactory);
        }

        private async Task<string> AbandonActiveRunAsync(string userId, RunService runService, CancellationToken cancellationToken)
        {
            var current = await runService.GetCurrentAsync(userId, cancellationToken);

            if (current.Run == null)
                return string.Empty;

            var result = await runService.AbandonAsync(userId, current.Run.Id, cancellationToken);

            return result.Succeeded ? string.Empty : "Active run could not be abandoned, try again.";
        }

        private async Task<IResult> ResetAsync(HttpContext httpContext, string userId, [FromServices] PlayerDataService playerDataService)
        {
            var deletion = await playerDataService.ResetProgressAsync(userId, ReadActor(httpContext), httpContext.RequestAborted);

            return Results.Ok(deletion);
        }

        private async Task<IResult> AbandonRunAsync(HttpContext httpContext, string userId, [FromServices] RunService runService)
        {
            var current = await runService.GetCurrentAsync(userId, httpContext.RequestAborted);

            if (current.Run == null)
                return Results.NotFound(new { error = "Player has no active run." });

            var result = await runService.AbandonAsync(userId, current.Run.Id, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Run was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(new { runId = current.Run.Id });
        }

        private IResult ToResult(PlayerUpdateResult result, PlayerResponseFactory playerResponseFactory)
        {
            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request, try again." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(playerResponseFactory.Create(result.Profile!));
        }

        private QaAccountResponse CreateAccount(UserDocument user)
        {
            var qa = user.Qa;

            return new QaAccountResponse
            {
                UserId = user.Id,
                IsQa = qa != null,
                Alias = qa == null ? string.Empty : qa.Alias,
                MarkedBy = qa == null ? string.Empty : qa.MarkedBy,
                MarkedAt = qa == null ? null : qa.MarkedAt,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
            };
        }

        private string ReadActor(HttpContext httpContext)
        {
            return httpContext.RequestServices.GetRequiredService<AdminActorReader>().Read(httpContext);
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Http;
using Server.Api.Security;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Players;
using Server.Runs;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class PlayerEndpoints
    {
        private const string StorageRequiredMessage = "Player profiles require Mongo storage.";

        private bool _isMongoEnabled;

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;

            _isMongoEnabled = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value.Enabled;

            if (authOptions.Enabled == false)
                return;

            application.MapGet(ApiRoutes.PlayerProfile, GetProfileAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            application.MapGet(ApiRoutes.PlayerCharacteristics, GetCharacteristicsAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Produces<PlayerCharacteristicsResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.PlayerEquipmentLevel, UpgradeEquipmentLevelAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Accepts<PlayerEquipmentRequest>("application/json")
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.PlayerSummonLevel, UpgradeSummonLevelAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Accepts<PlayerSummonRequest>("application/json")
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.PlayerSummonMastery, UpgradeSummonMasteryAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Accepts<PlayerSummonRequest>("application/json")
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);

            application.MapPost(ApiRoutes.PlayerReset, ResetProgressAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            application.MapDelete(ApiRoutes.PlayerAccount, DeleteAccountAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .Produces<PlayerDeletionResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status401Unauthorized);

            application.MapPost(ApiRoutes.PlayerLoadout, UpdateLoadoutAsync)
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy)
                .WithMetadata(new GameConfigRequiredMetadata())
                .Accepts<PlayerLoadoutRequest>("application/json")
                .Produces<PlayerProfileResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status409Conflict);
        }

        private async Task<IResult> GetProfileAsync(
            HttpContext httpContext,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var profileService = httpContext.RequestServices.GetRequiredService<PlayerProfileService>();
            var profile = await profileService.GetOrCreateAsync(userId, configDistributor, httpContext.RequestAborted);

            return Results.Ok(playerResponseFactory.Create(profile));
        }

        private async Task<IResult> ResetProgressAsync(
            HttpContext httpContext,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerDataService playerDataService,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            await playerDataService.ResetProgressAsync(userId, "player", httpContext.RequestAborted);

            var profileService = httpContext.RequestServices.GetRequiredService<PlayerProfileService>();
            var profile = await profileService.GetOrCreateAsync(userId, configDistributor, httpContext.RequestAborted);

            return Results.Ok(playerResponseFactory.Create(profile));
        }

        private async Task<IResult> DeleteAccountAsync(
            HttpContext httpContext,
            [FromServices] PlayerDataService playerDataService,
            [FromServices] PlayerIdentityReader playerIdentityReader)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var deletion = await playerDataService.DeleteAsync(userId, "player", httpContext.RequestAborted);

            return Results.Ok(new PlayerDeletionResponse
            {
                UserId = userId,
                Profiles = deletion.Profiles,
                Runs = deletion.Runs,
                Ledger = deletion.Ledger,
                Idempotency = deletion.Idempotency,
                Users = deletion.Users,
            });
        }

        private async Task<IResult> UpdateLoadoutAsync(
            HttpContext httpContext,
            [FromBody] PlayerLoadoutRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (request == null)
                return Results.BadRequest(new { error = "Request body is required." });

            var profileService = httpContext.RequestServices.GetRequiredService<PlayerProfileService>();
            var update = new LoadoutUpdate(request.CharacterId, request.Equipment, request.Summons);
            var result = await profileService.UpdateLoadoutAsync(userId, update, request.RequestId, configDistributor, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(playerResponseFactory.Create(result.Profile!));
        }

        private async Task<IResult> GetCharacteristicsAsync(
            HttpContext httpContext,
            [FromServices] PlayerCharacteristicsService playerCharacteristicsService,
            [FromServices] PlayerIdentityReader playerIdentityReader)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            var result = await playerCharacteristicsService.GetAsync(userId, httpContext.RequestAborted);

            if (result.Succeeded == false)
                return Results.BadRequest(new { error = result.Error });

            var characteristics = result.Characteristics!;

            return Results.Ok(new PlayerCharacteristicsResponse
            {
                Health = characteristics.Health,
                MaxHealth = characteristics.MaxHealth,
                Damage = characteristics.Damage,
                AttackMultiplier = characteristics.AttackMultiplier,
                Armor = characteristics.Armor,
                Defence = characteristics.Defence,
                Evasion = characteristics.Evasion,
                CriticalChance = characteristics.CriticalChance,
                CriticalMultiplier = characteristics.CriticalMultiplier,
                Combo1Chance = characteristics.Combo1Chance,
                Combo2Chance = characteristics.Combo2Chance,
                ComboMultiplier = characteristics.ComboMultiplier,
                CounterChance = characteristics.CounterChance,
                CounterMultiplier = characteristics.CounterMultiplier,
                Energy = characteristics.Energy,
                EnergyGain = characteristics.EnergyGain,
                MaxEnergy = characteristics.MaxEnergy,
                SkillMultiplier = characteristics.SkillMultiplier,
                Vampyrism = characteristics.Vampyrism,
                HealingBoost = characteristics.HealingBoost,
            });
        }

        private async Task<IResult> UpgradeEquipmentLevelAsync(
            HttpContext httpContext,
            [FromBody] PlayerEquipmentRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (request == null || string.IsNullOrWhiteSpace(request.InstanceId))
                return Results.BadRequest(new { error = "instanceId is required." });

            var progressionService = httpContext.RequestServices.GetRequiredService<PlayerProgressionService>();
            var result = await progressionService.UpgradeEquipmentLevelAsync(userId, request.InstanceId, request.RequestId, configDistributor, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(playerResponseFactory.Create(result.Profile!));
        }

        private async Task<IResult> UpgradeSummonLevelAsync(
            HttpContext httpContext,
            [FromBody] PlayerSummonRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            return await UpgradeAsync(httpContext, request, configDistributor, playerIdentityReader, playerResponseFactory, true);
        }

        private async Task<IResult> UpgradeSummonMasteryAsync(
            HttpContext httpContext,
            [FromBody] PlayerSummonRequest? request,
            [FromServices] IConfigDistributor configDistributor,
            [FromServices] PlayerIdentityReader playerIdentityReader,
            [FromServices] PlayerResponseFactory playerResponseFactory)
        {
            return await UpgradeAsync(httpContext, request, configDistributor, playerIdentityReader, playerResponseFactory, false);
        }

        private async Task<IResult> UpgradeAsync(
            HttpContext httpContext,
            PlayerSummonRequest? request,
            IConfigDistributor configDistributor,
            PlayerIdentityReader playerIdentityReader,
            PlayerResponseFactory playerResponseFactory,
            bool isLevel)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: StorageRequiredMessage, statusCode: StatusCodes.Status503ServiceUnavailable);

            var userId = playerIdentityReader.Read(httpContext.User);

            if (string.IsNullOrEmpty(userId))
                return Results.Unauthorized();

            if (request == null || request.SummonId <= 0)
                return Results.BadRequest(new { error = "summonId is required." });

            var progressionService = httpContext.RequestServices.GetRequiredService<PlayerProgressionService>();
            var result = isLevel
                ? await progressionService.UpgradeSummonLevelAsync(userId, request.SummonId, request.RequestId, configDistributor, httpContext.RequestAborted)
                : await progressionService.UpgradeSummonMasteryAsync(userId, request.SummonId, request.RequestId, configDistributor, httpContext.RequestAborted);

            if (result.Conflict)
                return Results.Json(new { error = "Profile was changed by another request." }, statusCode: StatusCodes.Status409Conflict);

            if (result.Succeeded == false)
                return Results.BadRequest(new { errors = result.Errors });

            return Results.Ok(playerResponseFactory.Create(result.Profile!));
        }
    }
}

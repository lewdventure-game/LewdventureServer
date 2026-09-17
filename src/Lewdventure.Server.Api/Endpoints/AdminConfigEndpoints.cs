using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Endpoints
{
    internal sealed class AdminConfigEndpoints
    {
        private const string ActorPrefix = "admin:";
        private const int MaxListLimit = 100;

        public void Map(WebApplication application)
        {
            var adminOptions = application.Services.GetRequiredService<IOptions<AdminOptions>>().Value;
            var mongoOptions = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value;

            if (adminOptions.Enabled == false || mongoOptions.Enabled == false)
                return;

            var group = application.MapGroup(ApiRoutes.AdminConfig)
                .WithMetadata(new OpsPortOnlyMetadata())
                .RequireAuthorization(SecurityNames.AdminPolicy)
                .RequireRateLimiting(SecurityNames.AdminRateLimitPolicy);

            group.MapGet("/status", GetStatusAsync);
            group.MapGet("/snapshots", ListAsync);
            group.MapGet("/snapshots/{version}", ExportAsync);
            group.MapPost("/snapshots", UploadAsync);
            group.MapPost("/activate", ActivateAsync);
            group.MapPost("/reload", ReloadAsync);
        }

        private async Task<IResult> GetStatusAsync(
            HttpContext httpContext,
            [FromServices] ConfigPublishingService publishingService,
            [FromServices] ConfigResponseFactory responseFactory,
            [FromServices] IGameConfigSetProvider provider)
        {
            var activeVersion = await publishingService.GetActiveVersionAsync(httpContext.RequestAborted);

            return Results.Ok(responseFactory.CreateStatus(provider.Current, activeVersion));
        }

        private async Task<IResult> ListAsync(
            HttpContext httpContext,
            [FromQuery] int? limit,
            [FromServices] ConfigPublishingService publishingService)
        {
            var effectiveLimit = limit == null || limit.Value <= 0 || MaxListLimit < limit.Value ? 20 : limit.Value;
            var snapshots = await publishingService.ListAsync(effectiveLimit, httpContext.RequestAborted);

            return Results.Ok(snapshots);
        }

        private async Task<IResult> ExportAsync(
            HttpContext httpContext,
            string version,
            [FromServices] ConfigPublishingService publishingService,
            [FromServices] ConfigSnapshotSerializer serializer)
        {
            var snapshot = await publishingService.GetSnapshotAsync(version, httpContext.RequestAborted);

            if (snapshot == null)
                return Results.NotFound(new { error = $"Config snapshot {version} is not published." });

            return Results.Content(serializer.Serialize(snapshot), "application/json");
        }

        private async Task<IResult> UploadAsync(
            HttpContext httpContext,
            [FromQuery] bool? activate,
            [FromQuery] string? reason,
            [FromServices] ConfigPublishingService publishingService,
            [FromServices] ConfigResponseFactory responseFactory,
            [FromServices] ConfigSnapshotSerializer serializer)
        {
            using var streamReader = new StreamReader(httpContext.Request.Body);

            var text = await streamReader.ReadToEndAsync(httpContext.RequestAborted);
            GameConfigSnapshot snapshot;

            try
            {
                snapshot = serializer.Deserialize(text);
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is Newtonsoft.Json.JsonException)
            {
                return Results.BadRequest(new { error = exception.Message });
            }

            var result = await publishingService.PublishAsync(snapshot, CreateActor(httpContext), reason ?? string.Empty, activate == true, httpContext.RequestAborted);

            return responseFactory.CreatePublishResult(result);
        }

        private async Task<IResult> ActivateAsync(
            HttpContext httpContext,
            [FromBody] ConfigActionRequest request,
            [FromServices] ConfigPublishingService publishingService,
            [FromServices] ConfigResponseFactory responseFactory)
        {
            if (string.IsNullOrWhiteSpace(request.Version))
                return Results.BadRequest(new { error = "version is required." });

            var result = await publishingService.ActivateAsync(request.Version, CreateActor(httpContext), request.Reason, httpContext.RequestAborted);

            return responseFactory.CreatePublishResult(result);
        }

        private async Task<IResult> ReloadAsync(
            HttpContext httpContext,
            [FromServices] ConfigPublishingService publishingService,
            [FromServices] ConfigResponseFactory responseFactory,
            [FromServices] IOptions<GameConfigOptions> gameConfigOptions)
        {
            var result = await publishingService.LoadActiveAsync(gameConfigOptions.Value.PinnedVersion, httpContext.RequestAborted);

            return responseFactory.CreatePublishResult(result);
        }

        private string CreateActor(HttpContext httpContext)
        {
            var remoteAddress = httpContext.Connection.RemoteIpAddress;

            return ActorPrefix + (remoteAddress == null ? "unknown" : remoteAddress.ToString());
        }
    }
}

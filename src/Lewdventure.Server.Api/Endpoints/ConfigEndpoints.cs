using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.Api.Security;
using Server.GameConfigs;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Services;

namespace Server.Api.Endpoints
{
    internal sealed class ConfigEndpoints
    {
        private const string ActorPrefix = "config-publisher:";

        private bool _isMongoEnabled;

        public void Map(WebApplication application)
        {
            var publisherOptions = application.Services.GetRequiredService<IOptions<ConfigPublisherOptions>>().Value;

            _isMongoEnabled = application.Services.GetRequiredService<IOptions<MongoOptions>>().Value.Enabled;

            if (publisherOptions.Enabled == false)
                return;

            application.MapPost(ApiRoutes.ConfigUpload, UploadAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);

            application.MapGet(ApiRoutes.ConfigSheets, GetSheetsAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);

            application.MapGet(ApiRoutes.ConfigStatus, GetStatusAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);
        }

        private async Task<IResult> UploadAsync(
            HttpContext httpContext,
            [FromBody] ConfigUploadRequest? request,
            [FromServices] ConfigResponseFactory responseFactory,
            [FromServices] UploadedSheetsSnapshotBuilder snapshotBuilder)
        {
            if (_isMongoEnabled == false)
                return Results.Problem(detail: "Config upload requires Mongo storage.", statusCode: 503);

            if (request == null || request.Sheets.Count == 0)
                return Results.BadRequest(new { error = "Payload must contain sheets." });

            var uploaded = new List<UploadedSheet>(request.Sheets.Count);

            for (int i = 0; i < request.Sheets.Count; i++)
            {
                var sheet = request.Sheets[i];
                var values = new List<IReadOnlyList<object?>>(sheet.Values.Count);

                for (int j = 0; j < sheet.Values.Count; j++)
                    values.Add(sheet.Values[j]);

                uploaded.Add(new UploadedSheet(sheet.Domain, sheet.SpreadsheetId, sheet.Range, values));
            }

            if (snapshotBuilder.TryBuild(uploaded, out var snapshot, out var errors) == false)
            {
                var invalid = new ConfigPublishResult { Succeeded = false };

                invalid.Errors.AddRange(errors);

                return responseFactory.CreatePublishResult(invalid);
            }

            var publishingService = httpContext.RequestServices.GetRequiredService<ConfigPublishingService>();
            var result = await publishingService.PublishAsync(snapshot, CreateActor(httpContext), request.Reason, true, httpContext.RequestAborted);

            return responseFactory.CreatePublishResult(result);
        }

        private IResult GetSheetsAsync([FromServices] IOptions<ConfigSheetsOptions> configSheetsOptions, [FromServices] ConfigDomainNames configDomainNames)
        {
            var sheets = configSheetsOptions.Value.Sheets;
            var ordered = configDomainNames.Ordered;
            var payload = new List<object>(ordered.Count);

            for (int i = 0; i < ordered.Count; i++)
            {
                for (int j = 0; j < sheets.Count; j++)
                {
                    if (string.Equals(sheets[j].Domain, ordered[i], StringComparison.Ordinal) == false)
                        continue;

                    payload.Add(new { domain = sheets[j].Domain, spreadsheetId = sheets[j].SpreadsheetId });

                    break;
                }
            }

            return Results.Ok(new { sheets = payload });
        }

        private async Task<IResult> GetStatusAsync(
            HttpContext httpContext,
            [FromServices] ConfigResponseFactory responseFactory,
            [FromServices] IGameConfigSetProvider provider)
        {
            var activeVersion = string.Empty;

            if (_isMongoEnabled)
                activeVersion = await httpContext.RequestServices.GetRequiredService<ConfigPublishingService>().GetActiveVersionAsync(httpContext.RequestAborted);

            return Results.Ok(responseFactory.CreateStatus(provider.Current, activeVersion));
        }

        private string CreateActor(HttpContext httpContext)
        {
            var remoteAddress = httpContext.Connection.RemoteIpAddress;

            return ActorPrefix + (remoteAddress == null ? "unknown" : remoteAddress.ToString());
        }
    }
}

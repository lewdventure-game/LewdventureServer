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

            application.MapPost(ApiRoutes.PublishConfig, PublishAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);

            application.MapPost(ApiRoutes.UpdateConfig, UpdateAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);

            application.MapGet(ApiRoutes.ConfigStatus, GetStatusAsync)
                .RequireAuthorization(SecurityNames.ConfigPublisherPolicy)
                .RequireRateLimiting(SecurityNames.ConfigRateLimitPolicy);
        }

        private async Task<IResult> PublishAsync(
            HttpContext httpContext,
            [FromBody] ConfigActionRequest? request,
            [FromServices] ConfigResponseFactory responseFactory,
            [FromServices] IGameConfigService configService,
            [FromServices] IGameConfigSetProvider provider,
            IHostEnvironment environment)
        {
            if (_isMongoEnabled == false)
            {
                var (success, message) = await configService.UpdateAllConfigsAsync(environment.IsDevelopment());
                var fallbackResult = new ConfigPublishResult { Succeeded = success, Version = provider.Current.Version, Activated = success };

                if (success == false)
                    fallbackResult.Errors.Add(message);

                return responseFactory.CreatePublishResult(fallbackResult);
            }

            var publishingService = httpContext.RequestServices.GetRequiredService<ConfigPublishingService>();
            var reason = request == null ? string.Empty : request.Reason;
            var result = await publishingService.ImportAndPublishAsync(CreateActor(httpContext), reason, httpContext.RequestAborted);

            return responseFactory.CreatePublishResult(result);
        }

        private async Task<IResult> UpdateAsync(
            HttpContext httpContext,
            [FromServices] IGameConfigService configService,
            IHostEnvironment environment)
        {
            if (_isMongoEnabled == false)
            {
                var (success, message) = await configService.UpdateAllConfigsAsync(environment.IsDevelopment());

                return success
                    ? Results.Ok(new { status = "success", message })
                    : Results.Problem(detail: message, statusCode: 500);
            }

            var publishingService = httpContext.RequestServices.GetRequiredService<ConfigPublishingService>();
            var result = await publishingService.ImportAndPublishAsync(CreateActor(httpContext), "legacy update endpoint", httpContext.RequestAborted);

            return result.Succeeded
                ? Results.Ok(new { status = "success", message = $"version = {result.Version}" })
                : Results.Problem(detail: string.Join("; ", result.Errors), statusCode: 500);
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

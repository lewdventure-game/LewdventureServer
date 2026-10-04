using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Server.Api.Http;
using Server.Api.Security;
using Server.Infrastructure.GameConfigs;
using Server.Infrastructure.Players;

namespace Server.Api.Endpoints
{
    internal sealed class ConfigBundleEndpoints
    {
        private const string ContentType = "application/json";

        public void Map(WebApplication application)
        {
            var authOptions = application.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
            var builder = application.MapGet(ApiRoutes.ConfigBundle, Get)
                .WithMetadata(new GameConfigRequiredMetadata());

            if (authOptions.Enabled == false)
                return;

            builder
                .RequireAuthorization(SecurityNames.PlayerPolicy)
                .RequireRateLimiting(SecurityNames.PlayerRateLimitPolicy);
        }

        private IResult Get(
            HttpContext httpContext,
            [FromServices] ClientConfigBundleFactory clientConfigBundleFactory,
            [FromServices] EntityTagReader entityTagReader,
            [FromServices] GameConfigSelection gameConfigSelection)
        {
            var bundle = clientConfigBundleFactory.Create(gameConfigSelection.Current);

            httpContext.Response.Headers[HeaderNames.ETag] = bundle.EntityTag;
            httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache";

            if (IsNotModified(httpContext, entityTagReader, bundle))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return Results.Content(bundle.Json, ContentType);
        }

        private bool IsNotModified(HttpContext httpContext, EntityTagReader entityTagReader, ClientConfigBundle bundle)
        {
            var requestedTags = httpContext.Request.Headers[HeaderNames.IfNoneMatch];

            for (int i = 0; i < requestedTags.Count; i++)
            {
                if (entityTagReader.Matches(requestedTags[i]!, bundle.EntityTagValue))
                    return true;
            }

            return false;
        }
    }
}

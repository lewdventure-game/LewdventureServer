using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Server.Api.Http;
using Server.Api.Security;
using Server.GameConfigs;
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
            [FromServices] IGameConfigSetProvider gameConfigSetProvider)
        {
            var bundle = clientConfigBundleFactory.Create(gameConfigSetProvider.Current);

            httpContext.Response.Headers[HeaderNames.ETag] = bundle.EntityTag;
            httpContext.Response.Headers[HeaderNames.CacheControl] = "no-cache";

            if (IsNotModified(httpContext, bundle))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return Results.Content(bundle.Json, ContentType);
        }

        private bool IsNotModified(HttpContext httpContext, ClientConfigBundle bundle)
        {
            var requestedTags = httpContext.Request.Headers[HeaderNames.IfNoneMatch];

            for (int i = 0; i < requestedTags.Count; i++)
            {
                var requestedTag = requestedTags[i];

                if (string.IsNullOrEmpty(requestedTag))
                    continue;

                if (string.Equals(requestedTag, bundle.EntityTag, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}

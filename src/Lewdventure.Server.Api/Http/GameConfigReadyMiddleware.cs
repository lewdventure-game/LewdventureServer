using Server.GameConfigs;

namespace Server.Api.Http
{
    internal sealed class GameConfigReadyMiddleware
    {
        private const string NotLoadedBody = "{\"error\":\"Game configs are not loaded.\"}";

        private readonly RequestDelegate _next;

        public GameConfigReadyMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IGameConfigSetProvider gameConfigSetProvider)
        {
            var endpoint = context.GetEndpoint();

            if (endpoint == null || endpoint.Metadata.GetMetadata<GameConfigRequiredMetadata>() == null)
            {
                await _next(context);

                return;
            }

            if (gameConfigSetProvider.Current.IsEmpty == false)
            {
                await _next(context);

                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(NotLoadedBody);
        }
    }
}

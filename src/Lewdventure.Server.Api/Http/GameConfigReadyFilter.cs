using Server.GameConfigs;

namespace Server.Api.Http
{
    internal sealed class GameConfigReadyFilter
    {
        private const string NotLoadedBody = "{\"error\":\"Game configs are not loaded.\"}";

        private readonly IGameConfigSetProvider _gameConfigSetProvider;

        public GameConfigReadyFilter(IGameConfigSetProvider gameConfigSetProvider)
        {
            _gameConfigSetProvider = gameConfigSetProvider;
        }

        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            if (_gameConfigSetProvider.Current.IsEmpty)
                return Results.Content(NotLoadedBody, "application/json", null, StatusCodes.Status503ServiceUnavailable);

            return await next(context);
        }
    }
}

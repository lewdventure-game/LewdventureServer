using Server.Api.Security;
using Server.Infrastructure.Alerts;
using Server.Infrastructure.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Infrastructure.Players;

namespace Server.Api.Http
{
    internal sealed class GameConfigSelectionMiddleware
    {
        private readonly RequestDelegate _next;

        public GameConfigSelectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IAlertPublisher alertPublisher,
            GameConfigSelection gameConfigSelection,
            GameConfigSetCache gameConfigSetCache,
            ILogger<GameConfigSelectionMiddleware> logger,
            PlayerConfigVersionResolver playerConfigVersionResolver,
            PlayerIdentityReader playerIdentityReader)
        {
            var endpoint = context.GetEndpoint();

            if (endpoint == null || endpoint.Metadata.GetMetadata<GameConfigRequiredMetadata>() == null)
            {
                await _next(context);

                return;
            }

            var userId = playerIdentityReader.Read(context.User);

            if (string.IsNullOrEmpty(userId))
            {
                await _next(context);

                return;
            }

            var version = await playerConfigVersionResolver.ResolveAsync(userId, context.RequestAborted);

            if (string.IsNullOrEmpty(version) == false)
            {
                var configSet = await gameConfigSetCache.GetAsync(version, context.RequestAborted);

                if (configSet == null)
                    ReportFallback(alertPublisher, logger, userId, version);
                else
                    gameConfigSelection.Select(configSet);
            }

            await _next(context);
        }

        private void ReportFallback(IAlertPublisher alertPublisher, ILogger<GameConfigSelectionMiddleware> logger, string userId, string version)
        {
            logger.LogWarning("[Config][Snapshot] pinned version unavailable, master used userId = {UserId} version = {Version}", userId, version);

            var alert = new AlertMessage(AlertSeverity.Warning, "Pinned game configs unavailable", "Player request fell back to master configs.", $"config-pinned-missing:{version}");

            alert.Fields.Add(new KeyValuePair<string, string>("version", version));

            alertPublisher.Publish(alert);
        }
    }
}

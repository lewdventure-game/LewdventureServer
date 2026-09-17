using Server.Services;

namespace Server.Api.Hosting
{
    internal sealed class ConfigStartupSync
    {
        public async Task RunAsync(IServiceProvider services)
        {
            var configService = services.GetRequiredService<IGameConfigService>();
            var environment = services.GetRequiredService<IHostEnvironment>();
            var logger = services.GetRequiredService<ILogger<ConfigStartupSync>>();

            logger.LogInformation("[Config] startup sync begin (same path as POST /api/config/update)");

            var (success, message) = await configService.UpdateAllConfigsAsync(environment.IsDevelopment());

            if (success == false)
            {
                logger.LogError($"[Config] startup sync failed: {message}");

                return;
            }

            logger.LogInformation($"[Config] startup sync ok: {message}");
        }
    }
}

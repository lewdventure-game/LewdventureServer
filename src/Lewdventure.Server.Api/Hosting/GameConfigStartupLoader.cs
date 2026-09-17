using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.GameConfigs;
using Server.Services;

namespace Server.Api.Hosting
{
    internal sealed class GameConfigStartupLoader
    {
        public async Task RunAsync(IServiceProvider services)
        {
            var options = services.GetRequiredService<IOptions<GameConfigOptions>>().Value;
            var environment = services.GetRequiredService<IHostEnvironment>();
            var logger = services.GetRequiredService<ILogger<GameConfigStartupLoader>>();

            logger.LogInformation("[Config] startup load begin source = {Source}", options.Source);

            var (success, message) = options.Source == GameConfigSourceType.File
                ? await LoadFromFileAsync(services, options, environment)
                : await services.GetRequiredService<IGameConfigService>().UpdateAllConfigsAsync(environment.IsDevelopment());

            if (success)
            {
                logger.LogInformation($"[Config] startup load ok: {message}");

                return;
            }

            logger.LogError($"[Config] startup load failed: {message}");

            if (options.FailStartupIfUnavailable)
                throw new InvalidOperationException($"Game configs are unavailable at startup: {message}");
        }

        private async Task<(bool Success, string Message)> LoadFromFileAsync(IServiceProvider services, GameConfigOptions options, IHostEnvironment environment)
        {
            var source = services.GetRequiredService<FileConfigSnapshotSource>();
            var builder = services.GetRequiredService<GameConfigSetBuilder>();
            var provider = services.GetRequiredService<IGameConfigSetProvider>();
            var path = Path.IsPathRooted(options.FilePath) ? options.FilePath : Path.Combine(environment.ContentRootPath, options.FilePath);

            try
            {
                var snapshot = await source.LoadAsync(path, CancellationToken.None);
                var result = builder.Build(snapshot, FileConfigSnapshotSource.SourceKind);

                if (result.Succeeded == false)
                    return (false, string.Join("; ", result.Errors));

                provider.Swap(result.ConfigSet!);

                return (true, $"version = {snapshot.Version} path = {path}");
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is Newtonsoft.Json.JsonException)
            {
                return (false, exception.Message);
            }
        }
    }
}

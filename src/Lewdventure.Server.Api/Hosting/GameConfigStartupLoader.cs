using Microsoft.Extensions.Options;
using Server.Api.Options;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;
using Server.Services;

namespace Server.Api.Hosting
{
    internal sealed class GameConfigStartupLoader
    {
        private const string BootstrapActor = "startup-bootstrap";

        public async Task RunAsync(IServiceProvider services)
        {
            var options = services.GetRequiredService<IOptions<GameConfigOptions>>().Value;
            var environment = services.GetRequiredService<IHostEnvironment>();
            var logger = services.GetRequiredService<ILogger<GameConfigStartupLoader>>();

            logger.LogInformation("[Config] startup load begin source = {Source}", options.Source);

            var (success, message) = await LoadAsync(services, options, environment, logger);

            if (success)
            {
                logger.LogInformation($"[Config] startup load ok: {message}");

                return;
            }

            logger.LogError($"[Config] startup load failed: {message}");

            if (options.FailStartupIfUnavailable)
                throw new InvalidOperationException($"Game configs are unavailable at startup: {message}");
        }

        private async Task<(bool Success, string Message)> LoadAsync(IServiceProvider services, GameConfigOptions options, IHostEnvironment environment, ILogger logger)
        {
            if (options.Source == GameConfigSourceType.File)
                return await LoadFromFileAsync(services, ResolvePath(options.FilePath, environment));

            if (options.Source == GameConfigSourceType.Mongo)
                return await LoadFromMongoAsync(services, options, environment, logger);

            return await services.GetRequiredService<IGameConfigService>().UpdateAllConfigsAsync(environment.IsDevelopment());
        }

        private async Task<(bool Success, string Message)> LoadFromMongoAsync(IServiceProvider services, GameConfigOptions options, IHostEnvironment environment, ILogger logger)
        {
            var publishingService = services.GetRequiredService<ConfigPublishingService>();

            try
            {
                var result = await publishingService.LoadActiveAsync(options.PinnedVersion, CancellationToken.None);

                if (result.Succeeded == false && options.BootstrapFromGoogleSheetsIfEmpty && string.IsNullOrEmpty(result.Version))
                {
                    logger.LogWarning("[Config][Snapshot] no active snapshot; bootstrapping from Google Sheets");

                    result = await publishingService.ImportAndPublishAsync(BootstrapActor, "bootstrap on empty database", CancellationToken.None);
                }

                if (result.Succeeded == false)
                    return await LoadFromCacheAsync(services, options, environment, string.Join("; ", result.Errors));

                await WriteCacheAsync(services, options, environment, publishingService, logger);

                return (true, $"version = {services.GetRequiredService<IGameConfigSetProvider>().Current.Version} source = Mongo");
            }
            catch (Exception exception) when (exception is TimeoutException || exception is MongoDB.Driver.MongoException)
            {
                return await LoadFromCacheAsync(services, options, environment, exception.Message);
            }
        }

        private async Task<(bool Success, string Message)> LoadFromCacheAsync(IServiceProvider services, GameConfigOptions options, IHostEnvironment environment, string reason)
        {
            if (string.IsNullOrWhiteSpace(options.LocalCachePath))
                return (false, reason);

            var (success, message) = await LoadFromFileAsync(services, ResolvePath(options.LocalCachePath, environment));

            return success ? (true, $"loaded from local cache because Mongo failed: {reason}; {message}") : (false, $"{reason}; cache: {message}");
        }

        private async Task WriteCacheAsync(IServiceProvider services, GameConfigOptions options, IHostEnvironment environment, ConfigPublishingService publishingService, ILogger logger)
        {
            if (string.IsNullOrWhiteSpace(options.LocalCachePath))
                return;

            var version = services.GetRequiredService<IGameConfigSetProvider>().Current.Version;
            var snapshot = await publishingService.GetSnapshotAsync(version, CancellationToken.None);

            if (snapshot == null)
                return;

            try
            {
                await services.GetRequiredService<FileConfigSnapshotSource>().SaveAsync(ResolvePath(options.LocalCachePath, environment), snapshot, CancellationToken.None);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                logger.LogWarning("[Config][Snapshot] local cache write failed path = {Path} error = {Error}", options.LocalCachePath, exception.Message);
            }
        }

        private async Task<(bool Success, string Message)> LoadFromFileAsync(IServiceProvider services, string path)
        {
            var source = services.GetRequiredService<FileConfigSnapshotSource>();
            var builder = services.GetRequiredService<GameConfigSetBuilder>();
            var provider = services.GetRequiredService<IGameConfigSetProvider>();

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

        private string ResolvePath(string path, IHostEnvironment environment)
        {
            return Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path);
        }
    }
}

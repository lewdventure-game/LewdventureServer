using Server.GameConfigs;
using Server.Infrastructure.GoogleSheets;

namespace Server.Services
{
    internal sealed class GameConfigService : IGameConfigService
    {
        private const string SuccessMessage = "Конфиги успешно обновлены";

        private readonly GameConfigSetBuilder _gameConfigSetBuilder;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly GoogleSheetsConfigImporter _googleSheetsConfigImporter;
        private readonly ILogger<IGameConfigService> _logger;
        private readonly SemaphoreSlim _updateLock = new(1, 1);

        public GameConfigService(
            GameConfigSetBuilder gameConfigSetBuilder,
            IGameConfigSetProvider gameConfigSetProvider,
            GoogleSheetsConfigImporter googleSheetsConfigImporter,
            ILogger<IGameConfigService> logger)
        {
            _gameConfigSetBuilder = gameConfigSetBuilder;
            _gameConfigSetProvider = gameConfigSetProvider;
            _googleSheetsConfigImporter = googleSheetsConfigImporter;
            _logger = logger;
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateAllConfigsAsync(bool isDevEnvironment)
        {
            await _updateLock.WaitAsync();

            try
            {
                var snapshot = await _googleSheetsConfigImporter.ImportAsync(CancellationToken.None);
                var result = _gameConfigSetBuilder.Build(snapshot, GoogleSheetsConfigImporter.SourceKind);

                for (int i = 0; i < result.Warnings.Count; i++)
                    _logger.LogWarning("[Config][Snapshot] warning version = {Version} {Warning}", snapshot.Version, result.Warnings[i]);

                if (result.Succeeded == false)
                {
                    var errors = string.Join("; ", result.Errors);

                    _logger.LogError("[Config] update failed; keeping previous configs errors = {Errors}", errors);

                    return (false, $"Ошибка обновления: {errors}");
                }

                _gameConfigSetProvider.Swap(result.ConfigSet!);

                return (true, SuccessMessage);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"[Config] update failed; keeping previous configs message = {exception.Message}");

                return (false, $"Ошибка обновления: {exception.Message}");
            }
            finally
            {
                _updateLock.Release();
            }
        }
    }
}

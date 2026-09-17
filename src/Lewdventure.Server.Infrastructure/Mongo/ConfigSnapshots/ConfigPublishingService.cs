using Server.GameConfigs;
using Server.Infrastructure.GoogleSheets;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigPublishingService
    {
        private readonly ConfigActivationRepository _configActivationRepository;
        private readonly ConfigSnapshotDiff _configSnapshotDiff;
        private readonly ConfigSnapshotRepository _configSnapshotRepository;
        private readonly GameConfigSetBuilder _gameConfigSetBuilder;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly GoogleSheetsConfigImporter _googleSheetsConfigImporter;
        private readonly ILogger<ConfigPublishingService> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public ConfigPublishingService(
            ConfigActivationRepository configActivationRepository,
            ConfigSnapshotDiff configSnapshotDiff,
            ConfigSnapshotRepository configSnapshotRepository,
            GameConfigSetBuilder gameConfigSetBuilder,
            IGameConfigSetProvider gameConfigSetProvider,
            GoogleSheetsConfigImporter googleSheetsConfigImporter,
            ILogger<ConfigPublishingService> logger)
        {
            _configActivationRepository = configActivationRepository;
            _configSnapshotDiff = configSnapshotDiff;
            _configSnapshotRepository = configSnapshotRepository;
            _gameConfigSetBuilder = gameConfigSetBuilder;
            _gameConfigSetProvider = gameConfigSetProvider;
            _googleSheetsConfigImporter = googleSheetsConfigImporter;
            _logger = logger;
        }

        public async Task<ConfigPublishResult> ImportAndPublishAsync(string actor, string reason, CancellationToken cancellationToken)
        {
            var snapshot = await _googleSheetsConfigImporter.ImportAsync(cancellationToken);

            return await PublishAsync(snapshot, actor, reason, true, cancellationToken);
        }

        public async Task<ConfigPublishResult> PublishAsync(GameConfigSnapshot snapshot, string actor, string reason, bool activate, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);

            try
            {
                var result = new ConfigPublishResult { Version = snapshot.Version };
                var buildResult = _gameConfigSetBuilder.Build(snapshot, snapshot.SourceKind);

                result.Warnings.AddRange(buildResult.Warnings);

                if (buildResult.Succeeded == false)
                {
                    result.Errors.AddRange(buildResult.Errors);

                    _logger.LogWarning("[Config][Snapshot] publish rejected version = {Version} errors = {Errors}", snapshot.Version, string.Join("; ", buildResult.Errors));

                    return result;
                }

                result.Stored = await _configSnapshotRepository.InsertIfMissingAsync(snapshot, actor, cancellationToken);

                var activeVersion = await _configActivationRepository.GetActiveVersionAsync(cancellationToken);
                var activeSnapshot = string.IsNullOrEmpty(activeVersion) ? null : await _configSnapshotRepository.GetAsync(activeVersion, cancellationToken);

                result.PreviousVersion = activeVersion;
                result.Changes.AddRange(_configSnapshotDiff.Compare(activeSnapshot, snapshot));

                _logger.LogInformation("[Config][Snapshot] published version = {Version} stored = {Stored} by = {Actor}", snapshot.Version, result.Stored, actor);

                if (activate)
                    await ActivateBuiltAsync(result, buildResult.ConfigSet!, actor, reason, cancellationToken);

                result.Succeeded = true;

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ConfigPublishResult> ActivateAsync(string version, string actor, string reason, CancellationToken cancellationToken)
        {
            await _lock.WaitAsync(cancellationToken);

            try
            {
                var result = new ConfigPublishResult { Version = version };
                var snapshot = await _configSnapshotRepository.GetAsync(version, cancellationToken);

                if (snapshot == null)
                {
                    result.Errors.Add($"Config snapshot {version} is not published.");

                    return result;
                }

                var buildResult = _gameConfigSetBuilder.Build(snapshot, snapshot.SourceKind);

                result.Warnings.AddRange(buildResult.Warnings);

                if (buildResult.Succeeded == false)
                {
                    result.Errors.AddRange(buildResult.Errors);

                    return result;
                }

                var activeVersion = await _configActivationRepository.GetActiveVersionAsync(cancellationToken);
                var activeSnapshot = string.IsNullOrEmpty(activeVersion) ? null : await _configSnapshotRepository.GetAsync(activeVersion, cancellationToken);

                result.PreviousVersion = activeVersion;
                result.Changes.AddRange(_configSnapshotDiff.Compare(activeSnapshot, snapshot));

                await ActivateBuiltAsync(result, buildResult.ConfigSet!, actor, reason, cancellationToken);

                result.Succeeded = true;

                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ConfigPublishResult> LoadActiveAsync(string pinnedVersion, CancellationToken cancellationToken)
        {
            var result = new ConfigPublishResult();
            var version = string.IsNullOrWhiteSpace(pinnedVersion) ? await _configActivationRepository.GetActiveVersionAsync(cancellationToken) : pinnedVersion;

            result.Version = version;

            if (string.IsNullOrEmpty(version))
            {
                result.Errors.Add("No active config snapshot in MongoDB.");

                return result;
            }

            if (string.Equals(_gameConfigSetProvider.Current.Version, version, StringComparison.Ordinal))
            {
                result.Succeeded = true;

                return result;
            }

            var snapshot = await _configSnapshotRepository.GetAsync(version, cancellationToken);

            if (snapshot == null)
            {
                result.Errors.Add($"Config snapshot {version} is not published.");

                return result;
            }

            var buildResult = _gameConfigSetBuilder.Build(snapshot, "Mongo");

            result.Warnings.AddRange(buildResult.Warnings);

            if (buildResult.Succeeded == false)
            {
                result.Errors.AddRange(buildResult.Errors);

                return result;
            }

            _gameConfigSetProvider.Swap(buildResult.ConfigSet!);

            result.Succeeded = true;

            return result;
        }

        public async Task<GameConfigSnapshot?> GetSnapshotAsync(string version, CancellationToken cancellationToken)
        {
            return await _configSnapshotRepository.GetAsync(version, cancellationToken);
        }

        public async Task<List<ConfigSnapshotSummary>> ListAsync(int limit, CancellationToken cancellationToken)
        {
            return await _configSnapshotRepository.ListAsync(limit, cancellationToken);
        }

        public async Task<string> GetActiveVersionAsync(CancellationToken cancellationToken)
        {
            return await _configActivationRepository.GetActiveVersionAsync(cancellationToken);
        }

        private async Task ActivateBuiltAsync(ConfigPublishResult result, GameConfigSet configSet, string actor, string reason, CancellationToken cancellationToken)
        {
            if (string.Equals(result.PreviousVersion, configSet.Version, StringComparison.Ordinal) == false)
            {
                await _configActivationRepository.ActivateAsync(configSet.Version, actor, reason, cancellationToken);

                result.Activated = true;

                _logger.LogInformation("[Config][Snapshot] activated version = {Version} previous = {PreviousVersion} by = {Actor} reason = {Reason}", configSet.Version, result.PreviousVersion, actor, reason);
            }

            if (string.Equals(_gameConfigSetProvider.Current.Version, configSet.Version, StringComparison.Ordinal) == false)
                _gameConfigSetProvider.Swap(configSet);
        }
    }
}

using Server.GameConfigs;

namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class GameConfigSetCache
    {
        public const int Capacity = 8;
        public const int FailureRetrySeconds = 60;

        private readonly ConfigSnapshotRepository _configSnapshotRepository;
        private readonly GameConfigSetBuilder _gameConfigSetBuilder;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly ILogger<GameConfigSetCache> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly Dictionary<string, CachedGameConfigSet> _entries = new(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTimeOffset> _failures = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private readonly object _sync = new();

        private GameConfigSet? _knownMaster;
        private long _useCounter;

        public GameConfigSetCache(
            ConfigSnapshotRepository configSnapshotRepository,
            GameConfigSetBuilder gameConfigSetBuilder,
            IGameConfigSetProvider gameConfigSetProvider,
            ILogger<GameConfigSetCache> logger,
            TimeProvider timeProvider)
        {
            _configSnapshotRepository = configSnapshotRepository;
            _gameConfigSetBuilder = gameConfigSetBuilder;
            _gameConfigSetProvider = gameConfigSetProvider;
            _logger = logger;
            _timeProvider = timeProvider;
        }

        public async Task<GameConfigSet?> GetAsync(string version, CancellationToken cancellationToken)
        {
            var master = _gameConfigSetProvider.Current;

            if (string.Equals(master.Version, version, StringComparison.Ordinal))
                return master;

            if (TryGetCached(master, version, out var cached))
                return cached;

            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                if (TryGetCached(master, version, out cached))
                    return cached;

                if (IsRecentlyFailed(version))
                    return null;

                var loaded = await LoadAsync(version, cancellationToken);

                if (loaded == null)
                    RememberFailure(version);
                else
                    Store(loaded);

                return loaded;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private bool TryGetCached(GameConfigSet master, string version, out GameConfigSet? configSet)
        {
            lock (_sync)
            {
                RememberPreviousMaster(master);

                if (_entries.TryGetValue(version, out var entry) == false)
                {
                    configSet = null;

                    return false;
                }

                _useCounter += 1;
                entry.LastUsed = _useCounter;
                configSet = entry.ConfigSet;

                return true;
            }
        }

        private void RememberPreviousMaster(GameConfigSet master)
        {
            if (_knownMaster == null)
            {
                _knownMaster = master;

                return;
            }

            if (ReferenceEquals(_knownMaster, master))
                return;

            var previous = _knownMaster;

            _knownMaster = master;
            _entries.Remove(master.Version);

            if (previous.IsEmpty)
                return;

            AddEntry(previous);
        }

        private async Task<GameConfigSet?> LoadAsync(string version, CancellationToken cancellationToken)
        {
            var snapshot = await _configSnapshotRepository.GetAsync(version, cancellationToken);

            if (snapshot == null)
            {
                _logger.LogWarning("[Config][Snapshot] cache load failed version = {Version} error = snapshot is not published", version);

                return null;
            }

            var buildResult = _gameConfigSetBuilder.Build(snapshot, "Mongo");

            if (buildResult.Succeeded == false)
            {
                _logger.LogWarning("[Config][Snapshot] cache load failed version = {Version} errors = {Errors}", version, string.Join("; ", buildResult.Errors));

                return null;
            }

            _logger.LogInformation("[Config][Snapshot] cached version = {Version}", version);

            return buildResult.ConfigSet;
        }

        private bool IsRecentlyFailed(string version)
        {
            lock (_sync)
            {
                if (_failures.TryGetValue(version, out var failedAt) == false)
                    return false;

                if (failedAt.AddSeconds(FailureRetrySeconds) <= _timeProvider.GetUtcNow())
                {
                    _failures.Remove(version);

                    return false;
                }

                return true;
            }
        }

        private void RememberFailure(string version)
        {
            lock (_sync)
            {
                _failures[version] = _timeProvider.GetUtcNow();
            }
        }

        private void Store(GameConfigSet configSet)
        {
            lock (_sync)
            {
                AddEntry(configSet);
            }
        }

        private void AddEntry(GameConfigSet configSet)
        {
            _useCounter += 1;
            _entries[configSet.Version] = new CachedGameConfigSet(configSet, _useCounter);

            while (Capacity < _entries.Count)
                EvictLeastRecentlyUsed();
        }

        private void EvictLeastRecentlyUsed()
        {
            var evictedVersion = string.Empty;
            var evictedLastUsed = long.MaxValue;

            foreach (var pair in _entries)
            {
                if (evictedLastUsed <= pair.Value.LastUsed)
                    continue;

                evictedVersion = pair.Key;
                evictedLastUsed = pair.Value.LastUsed;
            }

            _entries.Remove(evictedVersion);

            _logger.LogInformation("[Config][Snapshot] evicted version = {Version}", evictedVersion);
        }
    }
}

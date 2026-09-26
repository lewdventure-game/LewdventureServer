using Server.GameConfigs;

namespace Server.ConfigTool
{
    internal sealed class ConfigToolCommands
    {
        private readonly ConfigSnapshotDiff _configSnapshotDiff;
        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly FileConfigSnapshotSource _fileConfigSnapshotSource;
        private readonly GameConfigSetBuilder _gameConfigSetBuilder;

        public ConfigToolCommands(
            ConfigSnapshotDiff configSnapshotDiff,
            ConfigSnapshotHasher configSnapshotHasher,
            FileConfigSnapshotSource fileConfigSnapshotSource,
            GameConfigSetBuilder gameConfigSetBuilder)
        {
            _configSnapshotDiff = configSnapshotDiff;
            _configSnapshotHasher = configSnapshotHasher;
            _fileConfigSnapshotSource = fileConfigSnapshotSource;
            _gameConfigSetBuilder = gameConfigSetBuilder;
        }

        public async Task<int> ValidateAsync(string path)
        {
            var snapshot = await _fileConfigSnapshotSource.LoadAsync(path, CancellationToken.None);

            return Report(snapshot);
        }

        public async Task<int> HashAsync(string path)
        {
            var snapshot = await _fileConfigSnapshotSource.LoadAsync(path, CancellationToken.None);

            Console.WriteLine(snapshot.Version);

            return 0;
        }

        public async Task<int> DiffAsync(string fromPath, string toPath)
        {
            var before = await _fileConfigSnapshotSource.LoadAsync(fromPath, CancellationToken.None);
            var after = await _fileConfigSnapshotSource.LoadAsync(toPath, CancellationToken.None);
            var diffs = _configSnapshotDiff.Compare(before, after);

            Console.WriteLine($"from {_configSnapshotHasher.ToShortVersion(before.Version)} to {_configSnapshotHasher.ToShortVersion(after.Version)}");

            for (int i = 0; i < diffs.Count; i++)
            {
                var diff = diffs[i];

                if (diff.HasChanges == false)
                    continue;

                Console.WriteLine($"{diff.Domain}: rows {diff.RowsBefore} -> {diff.RowsAfter} added [{string.Join(", ", diff.Added)}] removed [{string.Join(", ", diff.Removed)}] changed [{string.Join(", ", diff.Changed)}]");
            }

            return 0;
        }

        private int Report(GameConfigSnapshot snapshot)
        {
            var result = _gameConfigSetBuilder.Build(snapshot, snapshot.SourceKind);

            Console.WriteLine($"version {snapshot.Version} ({_configSnapshotHasher.ToShortVersion(snapshot.Version)})");

            for (int i = 0; i < result.Warnings.Count; i++)
                Console.WriteLine($"warning: {result.Warnings[i]}");

            for (int i = 0; i < result.Errors.Count; i++)
                Console.Error.WriteLine($"error: {result.Errors[i]}");

            return result.Succeeded ? 0 : 1;
        }
    }
}

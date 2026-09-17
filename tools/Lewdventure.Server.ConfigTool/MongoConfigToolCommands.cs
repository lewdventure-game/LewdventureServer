using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.ConfigTool
{
    internal sealed class MongoConfigToolCommands
    {
        private readonly ConfigPublishingService _configPublishingService;
        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly FileConfigSnapshotSource _fileConfigSnapshotSource;

        public MongoConfigToolCommands(
            ConfigPublishingService configPublishingService,
            ConfigSnapshotHasher configSnapshotHasher,
            FileConfigSnapshotSource fileConfigSnapshotSource)
        {
            _configPublishingService = configPublishingService;
            _configSnapshotHasher = configSnapshotHasher;
            _fileConfigSnapshotSource = fileConfigSnapshotSource;
        }

        public async Task<int> PublishAsync(string path, bool activate, string reason)
        {
            var snapshot = await _fileConfigSnapshotSource.LoadAsync(path, CancellationToken.None);
            var result = await _configPublishingService.PublishAsync(snapshot, CreateActor(), reason, activate, CancellationToken.None);

            return Report(result);
        }

        public async Task<int> ActivateAsync(string version, string reason)
        {
            var result = await _configPublishingService.ActivateAsync(version, CreateActor(), reason, CancellationToken.None);

            return Report(result);
        }

        public async Task<int> ListAsync()
        {
            var active = await _configPublishingService.GetActiveVersionAsync(CancellationToken.None);
            var snapshots = await _configPublishingService.ListAsync(50, CancellationToken.None);

            for (int i = 0; i < snapshots.Count; i++)
            {
                var snapshot = snapshots[i];
                var marker = string.Equals(snapshot.Version, active, StringComparison.Ordinal) ? "*" : " ";

                Console.WriteLine($"{marker} {_configSnapshotHasher.ToShortVersion(snapshot.Version)} {snapshot.CreatedAt:yyyy-MM-dd HH:mm:ss} {snapshot.SourceKind} {snapshot.CreatedBy} {snapshot.Version}");
            }

            return 0;
        }

        public async Task<int> ExportAsync(string version, string outputPath)
        {
            var snapshot = await _configPublishingService.GetSnapshotAsync(version, CancellationToken.None);

            if (snapshot == null)
            {
                Console.Error.WriteLine($"snapshot {version} is not published");

                return 1;
            }

            await _fileConfigSnapshotSource.SaveAsync(outputPath, snapshot, CancellationToken.None);

            Console.WriteLine($"saved {outputPath}");

            return 0;
        }

        public async Task<int> StatusAsync()
        {
            var active = await _configPublishingService.GetActiveVersionAsync(CancellationToken.None);

            Console.WriteLine(string.IsNullOrEmpty(active) ? "no active snapshot" : $"active {active}");

            return 0;
        }

        private int Report(ConfigPublishResult result)
        {
            Console.WriteLine($"version {result.Version} stored {result.Stored} activated {result.Activated} previous {result.PreviousVersion}");

            for (int i = 0; i < result.Changes.Count; i++)
            {
                var change = result.Changes[i];

                if (change.HasChanges)
                    Console.WriteLine($"{change.Domain}: rows {change.RowsBefore} -> {change.RowsAfter} added [{string.Join(", ", change.Added)}] removed [{string.Join(", ", change.Removed)}] changed [{string.Join(", ", change.Changed)}]");
            }

            for (int i = 0; i < result.Errors.Count; i++)
                Console.Error.WriteLine($"error: {result.Errors[i]}");

            return result.Succeeded ? 0 : 1;
        }

        private string CreateActor()
        {
            return "config-tool:" + Environment.UserName;
        }
    }
}

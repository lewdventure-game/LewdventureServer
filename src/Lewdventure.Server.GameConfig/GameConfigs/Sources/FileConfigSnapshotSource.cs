namespace Server.GameConfigs
{
    internal sealed class FileConfigSnapshotSource
    {
        public const string SourceKind = "File";

        private readonly ConfigSnapshotSerializer _configSnapshotSerializer;

        public FileConfigSnapshotSource(ConfigSnapshotSerializer configSnapshotSerializer)
        {
            _configSnapshotSerializer = configSnapshotSerializer;
        }

        public async Task<GameConfigSnapshot> LoadAsync(string path, CancellationToken cancellationToken)
        {
            if (File.Exists(path) == false)
                throw new FileNotFoundException($"Config snapshot file not found path = {path}", path);

            var text = await File.ReadAllTextAsync(path, cancellationToken);

            return _configSnapshotSerializer.Deserialize(text);
        }

        public async Task SaveAsync(string path, GameConfigSnapshot snapshot, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory) == false)
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(path, _configSnapshotSerializer.Serialize(snapshot), cancellationToken);
        }
    }
}

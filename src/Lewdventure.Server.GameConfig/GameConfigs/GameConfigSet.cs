using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class GameConfigSet
    {
        public const string EmptyVersion = "empty";

        public GameConfigSet(string version, DateTime loadedAt, string source, IConfigDistributor distributor, GameConfigSnapshot snapshot)
        {
            Version = version;
            LoadedAt = loadedAt;
            Source = source;
            Distributor = distributor;
            Snapshot = snapshot;
        }

        public string Version { get; }

        public DateTime LoadedAt { get; }

        public string Source { get; }

        public IConfigDistributor Distributor { get; }

        public GameConfigSnapshot Snapshot { get; }

        public bool IsEmpty => string.Equals(Version, EmptyVersion, StringComparison.Ordinal);
    }
}

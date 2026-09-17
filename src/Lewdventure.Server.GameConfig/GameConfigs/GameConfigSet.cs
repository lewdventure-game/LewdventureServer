using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class GameConfigSet
    {
        public const string EmptyVersion = "empty";

        public GameConfigSet(string version, DateTime loadedAt, string source, IConfigDistributor distributor)
        {
            Version = version;
            LoadedAt = loadedAt;
            Source = source;
            Distributor = distributor;
        }

        public string Version { get; }

        public DateTime LoadedAt { get; }

        public string Source { get; }

        public IConfigDistributor Distributor { get; }

        public bool IsEmpty => string.Equals(Version, EmptyVersion, StringComparison.Ordinal);
    }
}

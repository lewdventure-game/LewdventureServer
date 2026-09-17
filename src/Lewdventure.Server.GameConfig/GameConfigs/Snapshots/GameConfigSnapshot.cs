namespace Server.GameConfigs
{
    internal sealed class GameConfigSnapshot
    {
        public const int CurrentFormatVersion = 1;

        public GameConfigSnapshot(string version, DateTime createdAt, string sourceKind, IReadOnlyList<ConfigSnapshotDomain> domains)
        {
            Version = version;
            CreatedAt = createdAt;
            SourceKind = sourceKind;
            Domains = domains;
        }

        public string Version { get; }

        public DateTime CreatedAt { get; }

        public string SourceKind { get; }

        public IReadOnlyList<ConfigSnapshotDomain> Domains { get; }

        public bool TryGetDomain(string domainName, out ConfigSnapshotDomain domain)
        {
            for (int i = 0; i < Domains.Count; i++)
            {
                if (string.Equals(Domains[i].Domain, domainName, StringComparison.Ordinal))
                {
                    domain = Domains[i];

                    return true;
                }
            }

            domain = null!;

            return false;
        }
    }
}

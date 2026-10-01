using Newtonsoft.Json;
using Server.GameConfigs;

namespace Server.Api.Endpoints
{
    internal sealed class ClientConfigBundleFactory
    {
        private readonly ConfigSnapshotHasher _configSnapshotHasher;

        private ClientConfigBundle? _cached;

        public ClientConfigBundleFactory(ConfigSnapshotHasher configSnapshotHasher)
        {
            _configSnapshotHasher = configSnapshotHasher;
        }

        public ClientConfigBundle Create(GameConfigSet configSet)
        {
            var cached = Volatile.Read(ref _cached);

            if (cached != null && string.Equals(cached.Version, configSet.Version, StringComparison.Ordinal))
                return cached;

            var bundle = Build(configSet);

            Interlocked.Exchange(ref _cached, bundle);

            return bundle;
        }

        private ClientConfigBundle Build(GameConfigSet configSet)
        {
            var domains = configSet.Snapshot.Domains;
            var entries = new List<ClientConfigBundleEntry>(domains.Count);

            for (int i = 0; i < domains.Count; i++)
                entries.Add(new ClientConfigBundleEntry { Name = domains[i].Domain, Content = domains[i].RowsJson });

            var payload = new ClientConfigBundlePayload
            {
                Version = configSet.Version,
                ShortVersion = _configSnapshotHasher.ToShortVersion(configSet.Version),
                Configs = entries,
            };

            return new ClientConfigBundle(
                configSet.Version,
                "\"" + _configSnapshotHasher.ToShortVersion(configSet.Version) + "\"",
                JsonConvert.SerializeObject(payload));
        }
    }
}

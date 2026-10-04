using Newtonsoft.Json;
using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Endpoints
{
    internal sealed class ClientConfigBundleFactory
    {
        private const int Capacity = GameConfigSetCache.Capacity + 1;

        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly Dictionary<string, ClientConfigBundle> _bundles = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        public ClientConfigBundleFactory(ConfigSnapshotHasher configSnapshotHasher)
        {
            _configSnapshotHasher = configSnapshotHasher;
        }

        public ClientConfigBundle Create(GameConfigSet configSet)
        {
            lock (_sync)
            {
                if (_bundles.TryGetValue(configSet.Version, out var cached))
                    return cached;
            }

            var bundle = Build(configSet);

            lock (_sync)
            {
                if (Capacity <= _bundles.Count)
                    _bundles.Clear();

                _bundles[configSet.Version] = bundle;
            }

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
                _configSnapshotHasher.ToShortVersion(configSet.Version),
                JsonConvert.SerializeObject(payload));
        }
    }
}

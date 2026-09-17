using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotDiff
    {
        private const string IdField = "id";

        private readonly ConfigDomainNames _configDomainNames;

        public ConfigSnapshotDiff(ConfigDomainNames configDomainNames)
        {
            _configDomainNames = configDomainNames;
        }

        public List<ConfigDomainDiff> Compare(GameConfigSnapshot? before, GameConfigSnapshot after)
        {
            var result = new List<ConfigDomainDiff>();
            var domains = _configDomainNames.Ordered;

            for (int i = 0; i < domains.Count; i++)
            {
                var beforeRows = LoadRows(before, domains[i]);
                var afterRows = LoadRows(after, domains[i]);
                var diff = new ConfigDomainDiff(domains[i], beforeRows.Count, afterRows.Count);

                foreach (var pair in afterRows)
                {
                    if (beforeRows.TryGetValue(pair.Key, out var beforeRow) == false)
                        diff.Added.Add(pair.Key);
                    else if (string.Equals(beforeRow, pair.Value, StringComparison.Ordinal) == false)
                        diff.Changed.Add(pair.Key);
                }

                foreach (var pair in beforeRows)
                {
                    if (afterRows.ContainsKey(pair.Key) == false)
                        diff.Removed.Add(pair.Key);
                }

                result.Add(diff);
            }

            return result;
        }

        private Dictionary<string, string> LoadRows(GameConfigSnapshot? snapshot, string domainName)
        {
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);

            if (snapshot == null || snapshot.TryGetDomain(domainName, out var domain) == false)
                return rows;

            var array = JArray.Parse(domain.RowsJson);

            for (int i = 0; i < array.Count; i++)
            {
                var row = array[i];
                var key = row is JObject rowObject && rowObject.TryGetValue(IdField, out var idToken) ? idToken.ToString() : $"#{i}";

                rows[key] = row.ToString(Formatting.None);
            }

            return rows;
        }
    }
}

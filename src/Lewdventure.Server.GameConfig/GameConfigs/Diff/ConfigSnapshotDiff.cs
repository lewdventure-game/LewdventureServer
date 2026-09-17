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
                var beforeRows = LoadRows(before, domains[i], out var beforeCount);
                var afterRows = LoadRows(after, domains[i], out var afterCount);
                var diff = new ConfigDomainDiff(domains[i], beforeCount, afterCount);

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

        private Dictionary<string, string> LoadRows(GameConfigSnapshot? snapshot, string domainName, out int count)
        {
            var rows = new Dictionary<string, string>(StringComparer.Ordinal);
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);

            count = 0;

            if (snapshot == null || snapshot.TryGetDomain(domainName, out var domain) == false)
                return rows;

            var array = JArray.Parse(domain.RowsJson);

            count = array.Count;

            for (int i = 0; i < array.Count; i++)
            {
                var row = array[i];
                var id = row is JObject rowObject && rowObject.TryGetValue(IdField, out var idToken) ? idToken.ToString() : string.Empty;
                var baseKey = string.IsNullOrEmpty(id) ? "#" : id;
                var occurrence = occurrences.TryGetValue(baseKey, out var seen) ? seen + 1 : 1;

                occurrences[baseKey] = occurrence;
                rows[occurrence == 1 && string.IsNullOrEmpty(id) == false ? id : $"{baseKey}#{occurrence}"] = row.ToString(Formatting.None);
            }

            return rows;
        }
    }
}

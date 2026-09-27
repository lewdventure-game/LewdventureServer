using Newtonsoft.Json.Linq;

namespace Server.GameConfigs
{
    internal sealed class EffectParametersValidator
    {
        private const string IdProperty = "id";
        private const string PerkTypeProperty = "perk_type";
        private const string PerkParametersProperty = "perk_parameters";
        private const string StatusTypeProperty = "status_type";
        private const string StatusParametersProperty = "parameters";

        private readonly EffectParameterRegistry _effectParameterRegistry;

        public EffectParametersValidator(EffectParameterRegistry effectParameterRegistry)
        {
            _effectParameterRegistry = effectParameterRegistry;
        }

        public void CollectPerks(ConfigSnapshotDomain domain, List<string> warnings)
        {
            Collect(domain, PerkTypeProperty, PerkParametersProperty, true, warnings);
        }

        public void CollectStatuses(ConfigSnapshotDomain domain, List<string> warnings)
        {
            Collect(domain, StatusTypeProperty, StatusParametersProperty, false, warnings);
        }

        private void Collect(ConfigSnapshotDomain domain, string typeProperty, string parametersProperty, bool isPerk, List<string> warnings)
        {
            JArray rows;

            try
            {
                rows = JArray.Parse(domain.RowsJson);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is JObject row == false)
                    continue;

                var id = ReadString(row, IdProperty);
                var effectType = ReadString(row, typeProperty);

                if (string.IsNullOrWhiteSpace(effectType))
                    continue;

                var found = isPerk
                    ? _effectParameterRegistry.TryGetPerk(effectType, out var descriptor)
                    : _effectParameterRegistry.TryGetStatus(effectType, out descriptor);

                if (found == false)
                {
                    warnings.Add($"Лист {domain.Domain}, строка id {id}: тип {effectType} сервером не поддерживается, механика не сработает.");

                    continue;
                }

                var keys = ReadKeys(ReadString(row, parametersProperty));

                CollectMissing(domain.Domain, id, effectType, descriptor, keys, warnings);
                CollectUnknown(domain.Domain, id, effectType, descriptor, keys, warnings);
            }
        }

        private void CollectMissing(
            string domainName,
            string id,
            string effectType,
            EffectParameterDescriptor descriptor,
            HashSet<string> keys,
            List<string> warnings)
        {
            var missing = new List<string>();

            for (int i = 0; i < descriptor.RequiredKeys.Length; i++)
            {
                if (keys.Contains(descriptor.RequiredKeys[i]) == false)
                    missing.Add(descriptor.RequiredKeys[i]);
            }

            if (missing.Count == 0)
                return;

            warnings.Add($"Лист {domainName}, строка id {id} ({effectType}): в параметрах нет обязательных ключей {string.Join(", ", missing)}.");
        }

        private void CollectUnknown(
            string domainName,
            string id,
            string effectType,
            EffectParameterDescriptor descriptor,
            HashSet<string> keys,
            List<string> warnings)
        {
            var unknown = new List<string>();

            foreach (var key in keys)
            {
                if (Contains(descriptor.RequiredKeys, key) || Contains(descriptor.OptionalKeys, key))
                    continue;

                unknown.Add(key);
            }

            if (unknown.Count == 0)
                return;

            warnings.Add($"Лист {domainName}, строка id {id} ({effectType}): параметры {string.Join(", ", unknown)} сервером не читаются, проверьте опечатку.");
        }

        private bool Contains(string[] values, string key)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (string.Equals(values[i], key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private HashSet<string> ReadKeys(string parameters)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(parameters))
                return keys;

            var pairs = parameters.Split(';', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i].Trim();
                var separator = pair.IndexOf(':');

                if (separator <= 0)
                    continue;

                keys.Add(pair.Substring(0, separator).Trim());
            }

            return keys;
        }

        private string ReadString(JObject row, string property)
        {
            var token = row[property];

            return token == null ? string.Empty : token.ToString();
        }
    }
}

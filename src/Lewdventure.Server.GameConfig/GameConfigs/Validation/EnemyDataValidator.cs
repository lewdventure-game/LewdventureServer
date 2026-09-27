using System.Globalization;
using Newtonsoft.Json.Linq;
using Server.Entities;

namespace Server.GameConfigs
{
    internal sealed class EnemyDataValidator
    {
        private const string IdProperty = "id";
        private const string CharacteristicsProperty = "other_characteristics";

        private readonly string[] _expectedKeys =
        {
            "defence",
            "evasion",
            "vampyrism",
            "healing_boost",
            "crit_chance",
            "crit_multiplier",
            "combo_1_chance",
            "combo_2_chance",
            "combo_multiplier",
            "counter_chance",
            "counter_multiplier",
            "spell_multiplier",
            "energy",
            "energy_max",
        };

        public void Collect(ConfigSnapshotDomain domain, List<string> warnings)
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
                var raw = ReadString(row, CharacteristicsProperty);

                if (string.IsNullOrWhiteSpace(raw))
                {
                    warnings.Add($"Лист {domain.Domain}, враг {id}: колонка {CharacteristicsProperty} пустая, все характеристики будут нулями.");

                    continue;
                }

                CollectMissingKeys(domain.Domain, id, raw, warnings);

                var packed = EnemyCharacteristics.Parse(raw);

                CollectChanceWithoutMultiplier(domain.Domain, id, "crit", packed.CriticalChance, packed.CriticalMultiplier, warnings);
                CollectChanceWithoutMultiplier(domain.Domain, id, "combo_1", packed.Combo1Chance, packed.Combo1Multiplier, warnings);
                CollectChanceWithoutMultiplier(domain.Domain, id, "combo_2", packed.Combo2Chance, packed.Combo2Multiplier, warnings);
                CollectChanceWithoutMultiplier(domain.Domain, id, "counter", packed.CounterChance, packed.CounterMultiplier, warnings);
            }
        }

        private void CollectMissingKeys(string domainName, string id, string raw, List<string> warnings)
        {
            var missing = new List<string>();

            for (int i = 0; i < _expectedKeys.Length; i++)
            {
                if (raw.Contains(_expectedKeys[i], StringComparison.OrdinalIgnoreCase) == false)
                    missing.Add(_expectedKeys[i]);
            }

            if (missing.Count == 0)
                return;

            warnings.Add($"Лист {domainName}, враг {id}: в {CharacteristicsProperty} нет ключей {string.Join(", ", missing)} — они считаются нулями.");
        }

        private void CollectChanceWithoutMultiplier(string domainName, string id, string mechanic, float chance, float multiplier, List<string> warnings)
        {
            if (chance <= 0f || 0f < multiplier)
                return;

            warnings.Add($"Лист {domainName}, враг {id}: {mechanic}_chance = {chance.ToString(CultureInfo.InvariantCulture)}, но множитель равен нулю — механика не нанесёт урона.");
        }

        private string ReadString(JObject row, string property)
        {
            var token = row[property];

            return token == null ? string.Empty : token.ToString();
        }
    }
}

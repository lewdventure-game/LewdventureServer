using System.Globalization;
using Newtonsoft.Json.Linq;
using Server.Entities;

namespace Server.GameConfigs
{
    internal sealed class EnemyDataValidator
    {
        private const string IdProperty = "id";
        private const string CharacteristicsProperty = "other_characteristics";
        private const string ArmorKey = "armor";
        private const string LegacyArmorKey = "defence";

        private readonly string[] _expectedKeys =
        {
            "armor",
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
        };

        private readonly string[] _obsoleteKeys =
        {
            "combo_1_multiplier",
            "combo_2_multiplier",
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
                CollectObsoleteKeys(domain.Domain, id, raw, warnings);

                var packed = new EnemyCharacteristics(raw);

                CollectChanceWithoutMultiplier(domain.Domain, id, "crit", packed.CriticalChance, packed.CriticalMultiplier, warnings);
                CollectChanceWithoutMultiplier(domain.Domain, id, "combo_1", packed.Combo1Chance, packed.ComboMultiplier, warnings);
                CollectChanceWithoutMultiplier(domain.Domain, id, "combo_2", packed.Combo2Chance, packed.ComboMultiplier, warnings);
                CollectChanceWithoutMultiplier(domain.Domain, id, "counter", packed.CounterChance, packed.CounterMultiplier, warnings);
            }
        }

        private void CollectMissingKeys(string domainName, string id, string raw, List<string> warnings)
        {
            var missing = new List<string>();

            for (int i = 0; i < _expectedKeys.Length; i++)
            {
                if (raw.Contains(_expectedKeys[i], StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.Equals(_expectedKeys[i], ArmorKey, StringComparison.Ordinal) && raw.Contains(LegacyArmorKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                missing.Add(_expectedKeys[i]);
            }

            if (missing.Count == 0)
                return;

            warnings.Add($"Лист {domainName}, враг {id}: в {CharacteristicsProperty} нет ключей {string.Join(", ", missing)} — они считаются нулями.");
        }

        private void CollectObsoleteKeys(string domainName, string id, string raw, List<string> warnings)
        {
            for (int i = 0; i < _obsoleteKeys.Length; i++)
            {
                if (raw.Contains(_obsoleteKeys[i], StringComparison.OrdinalIgnoreCase))
                    warnings.Add($"Лист {domainName}, враг {id}: ключ {_obsoleteKeys[i]} в {CharacteristicsProperty} не читается — множитель комбо один на оба удара, укажите combo_multiplier.");
            }

            if (raw.Contains(LegacyArmorKey, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Лист {domainName}, враг {id}: ключ {LegacyArmorKey} в {CharacteristicsProperty} устарел — укажите armor, ЗАЩИТА считается из БРОНИ по формуле 5.");
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

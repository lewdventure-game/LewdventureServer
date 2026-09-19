using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Bonuses;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Perks;
using Server.Services;
using Server.Skills;
using Server.Statuses;
using Server.Stories;

namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotValidator
    {
        private readonly ConfigDomainNames _configDomainNames;
        private readonly Dictionary<string, Type> _mapperTypes = new(StringComparer.Ordinal)
        {
            [ConfigDomainNames.Constants] = typeof(ConstantsMapper),
            [ConfigDomainNames.Characters] = typeof(CharacterMapper),
            [ConfigDomainNames.Bonuses] = typeof(BonusMapper),
            [ConfigDomainNames.Statuses] = typeof(StatusMapper),
            [ConfigDomainNames.Summons] = typeof(SummonMapper),
            [ConfigDomainNames.SummonLevels] = typeof(SummonLevelMapper),
            [ConfigDomainNames.Mastery] = typeof(MasteryMapper),
            [ConfigDomainNames.Enemies] = typeof(EnemyMapper),
            [ConfigDomainNames.Equipments] = typeof(EquipmentMapper),
            [ConfigDomainNames.StoryLevels] = typeof(StoryLevelMapper),
            [ConfigDomainNames.StoryStages] = typeof(StoryStageMapper),
            [ConfigDomainNames.StoryEvents] = typeof(StoryEventMapper),
            [ConfigDomainNames.ExpLevelsPatterns] = typeof(ExperienceLevelPatternMapper),
            [ConfigDomainNames.Perks] = typeof(PerkMapper),
            [ConfigDomainNames.PerkGroups] = typeof(PerkGroupMapper),
            [ConfigDomainNames.Skills] = typeof(SkillMapper),
        };

        public ConfigSnapshotValidator(ConfigDomainNames configDomainNames)
        {
            _configDomainNames = configDomainNames;
        }

        public void ValidateStructure(GameConfigSnapshot snapshot, List<string> errors, List<string> warnings)
        {
            var domains = _configDomainNames.Ordered;

            for (int i = 0; i < domains.Count; i++)
            {
                if (snapshot.TryGetDomain(domains[i], out var domain) == false)
                {
                    errors.Add($"Config snapshot domain {domains[i]} is missing.");

                    continue;
                }

                CollectHeaderWarnings(domain, warnings);
            }
        }

        public void ValidateContent(IConfigDistributor distributor, List<string> errors, List<string> warnings)
        {
            if (distributor.Constants.Collection.Count == 0)
                errors.Add("Config snapshot domain Constants has no rows.");

            if (distributor.Characters.Collection.Count == 0)
                errors.Add("Config snapshot domain Characters has no rows.");

            if (distributor.Enemies.Collection.Count == 0)
                errors.Add("Config snapshot domain Enemies has no rows.");

            if (distributor.StoryLevels.Collection.Count == 0)
                errors.Add("Config snapshot domain Story_levels has no rows.");

            if (distributor.Bonuses.Count == 0)
                warnings.Add("Config snapshot domain Bonuses has no rows.");
        }

        private void CollectHeaderWarnings(ConfigSnapshotDomain domain, List<string> warnings)
        {
            if (_mapperTypes.TryGetValue(domain.Domain, out var mapperType) == false)
                return;

            JArray rows;

            try
            {
                rows = JArray.Parse(domain.RowsJson);
            }
            catch (JsonReaderException exception)
            {
                warnings.Add($"Config snapshot domain {domain.Domain} rows are not valid JSON: {exception.Message}");

                return;
            }

            var headers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is JObject row == false)
                    continue;

                foreach (var property in row.Properties())
                    headers.Add(property.Name);
            }

            if (headers.Count == 0)
                return;

            var properties = mapperType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            for (int i = 0; i < properties.Length; i++)
            {
                var attribute = properties[i].GetCustomAttribute<JsonPropertyAttribute>();

                if (attribute == null || string.IsNullOrEmpty(attribute.PropertyName))
                    continue;

                if (headers.Contains(attribute.PropertyName) == false)
                    warnings.Add($"Config snapshot domain {domain.Domain} has no column {attribute.PropertyName} (range {domain.Range}).");
            }
        }
    }
}

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
        private const string ServiceColumn = "is_off";

        private readonly ConfigDomainNames _configDomainNames;
        private readonly EffectParametersValidator _effectParametersValidator;
        private readonly EnemyDataValidator _enemyDataValidator;
        private readonly SkillComponentValidator _skillComponentValidator;
        private readonly Dictionary<string, Type> _mapperTypes = new(StringComparer.Ordinal)
        {
            [ConfigDomainNames.Constants] = typeof(ConstantsMapper),
            [ConfigDomainNames.Characters] = typeof(CharacterMapper),
            [ConfigDomainNames.CharacterPromotes] = typeof(CharacterPromoteMapper),
            [ConfigDomainNames.Bonuses] = typeof(BonusMapper),
            [ConfigDomainNames.Statuses] = typeof(StatusMapper),
            [ConfigDomainNames.Summons] = typeof(SummonMapper),
            [ConfigDomainNames.SummonLevels] = typeof(SummonLevelMapper),
            [ConfigDomainNames.Mastery] = typeof(MasteryMapper),
            [ConfigDomainNames.Enemies] = typeof(EnemyMapper),
            [ConfigDomainNames.Equipments] = typeof(EquipmentMapper),
            [ConfigDomainNames.EquipmentPromotes] = typeof(EquipmentPromoteMapper),
            [ConfigDomainNames.StoryLevels] = typeof(StoryLevelMapper),
            [ConfigDomainNames.StoryStages] = typeof(StoryStageMapper),
            [ConfigDomainNames.StoryEvents] = typeof(StoryEventMapper),
            [ConfigDomainNames.ExpLevelsPatterns] = typeof(ExperienceLevelPatternMapper),
            [ConfigDomainNames.Perks] = typeof(PerkMapper),
            [ConfigDomainNames.PerkGroups] = typeof(PerkGroupMapper),
            [ConfigDomainNames.Skills] = typeof(SkillMapper),
        };

        public ConfigSnapshotValidator(
            ConfigDomainNames configDomainNames,
            EffectParametersValidator effectParametersValidator,
            EnemyDataValidator enemyDataValidator,
            SkillComponentValidator skillComponentValidator)
        {
            _configDomainNames = configDomainNames;
            _effectParametersValidator = effectParametersValidator;
            _enemyDataValidator = enemyDataValidator;
            _skillComponentValidator = skillComponentValidator;
        }

        public void ValidateStructure(GameConfigSnapshot snapshot, List<string> errors, List<string> warnings)
        {
            var domains = _configDomainNames.Ordered;

            for (int i = 0; i < domains.Count; i++)
            {
                if (snapshot.TryGetDomain(domains[i], out var domain) == false)
                {
                    if (_configDomainNames.IsOptional(domains[i]))
                        warnings.Add($"Лист {domains[i]} ещё не публиковался, сервер работает без него.");
                    else
                        errors.Add($"Config snapshot domain {domains[i]} is missing.");

                    continue;
                }

                CollectHeaderIssues(domain, errors, warnings);

                if (string.Equals(domains[i], ConfigDomainNames.Enemies, StringComparison.Ordinal))
                    _enemyDataValidator.Collect(domain, warnings);

                if (string.Equals(domains[i], ConfigDomainNames.Perks, StringComparison.Ordinal))
                    _effectParametersValidator.CollectPerks(domain, warnings);

                if (string.Equals(domains[i], ConfigDomainNames.Statuses, StringComparison.Ordinal))
                    _effectParametersValidator.CollectStatuses(domain, warnings);

                if (string.Equals(domains[i], ConfigDomainNames.Skills, StringComparison.Ordinal))
                {
                    _effectParametersValidator.CollectSkills(domain, warnings);
                    _skillComponentValidator.Validate(domain, errors, warnings);
                }
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

        private void CollectHeaderIssues(ConfigSnapshotDomain domain, List<string> errors, List<string> warnings)
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
                errors.Add($"Лист {domain.Domain}: строки не читаются как JSON: {exception.Message}");

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
            {
                warnings.Add($"Лист {domain.Domain}: нет ни одной строки с данными (диапазон {domain.Range}).");

                return;
            }

            var properties = mapperType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ServiceColumn };

            for (int i = 0; i < properties.Length; i++)
            {
                var attribute = properties[i].GetCustomAttribute<JsonPropertyAttribute>();

                if (attribute == null || string.IsNullOrEmpty(attribute.PropertyName))
                    continue;

                known.Add(attribute.PropertyName);

                if (headers.Contains(attribute.PropertyName))
                    continue;

                var optional = properties[i].GetCustomAttribute<OptionalColumnAttribute>();

                if (optional == null)
                    errors.Add($"Лист {domain.Domain}: нет обязательной колонки {attribute.PropertyName} (прочитан диапазон {domain.Range}).");
                else
                    warnings.Add($"Лист {domain.Domain}: нет необязательной колонки {attribute.PropertyName}: {optional.Reason}.");
            }

            foreach (var header in headers)
            {
                if (known.Contains(header) == false)
                    warnings.Add($"Лист {domain.Domain}: колонка {header} сервером не используется, проверьте опечатку.");
            }
        }
    }
}

using Core.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Server.Bonuses;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Perks;
using Server.Services;
using Server.Statuses;
using Server.Stories;

namespace Tests.Golden.Infrastructure
{
    internal sealed class ConfigDistributorFiller
    {
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly JsonSerializerSettings _rowsSettings;

        public ConfigDistributorFiller(IBonusWorkModeParser bonusWorkModeParser)
        {
            _bonusWorkModeParser = bonusWorkModeParser;
            _rowsSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new StringEnumConverter(new SnakeCaseNamingStrategy()) },
            };
        }

        public void Fill(IConfigDistributor distributor, ConfigSnapshotFile snapshot)
        {
            var constants = Parse<ConstantsMapper>(snapshot, "Constants");
            var characters = Parse<CharacterMapper>(snapshot, "Characters");
            var bonuses = Parse<BonusMapper>(snapshot, "Bonuses");
            var statuses = Parse<StatusMapper>(snapshot, "Statuses");
            var summons = Parse<SummonMapper>(snapshot, "Summons");
            var summonLevels = Parse<SummonLevelMapper>(snapshot, "Summon_levels");
            var masteries = Parse<MasteryMapper>(snapshot, "Mastery");
            var enemies = Parse<EnemyMapper>(snapshot, "Enemies");
            var equipments = Parse<EquipmentMapper>(snapshot, "Equipments");
            var storyLevels = Parse<StoryLevelMapper>(snapshot, "Story_levels");
            var storyStages = Parse<StoryStageMapper>(snapshot, "Story_stages");
            var storyEvents = Parse<StoryEventMapper>(snapshot, "Story_events");
            var experiencePatterns = Parse<ExperienceLevelPatternMapper>(snapshot, "Exp_levels_patterns");
            var perks = Parse<PerkMapper>(snapshot, "Perks");
            var perkGroups = Parse<PerkGroupMapper>(snapshot, "Perk_groups");

            distributor.ClearAll();

            AddList(distributor.Constants, constants);
            AddList(distributor.Characters, characters);
            AddBonuses(distributor, bonuses);
            AddList(distributor.Statuses, statuses);
            AddList(distributor.Summons, summons);
            AddList(distributor.SummonLevels, summonLevels);
            AddList(distributor.Masteries, masteries);
            AddList(distributor.Enemies, enemies);
            AddList(distributor.Equipments, equipments);
            AddList(distributor.StoryLevels, storyLevels);
            AddList(distributor.StoryStages, storyStages);
            AddList(distributor.StoryEvents, storyEvents);
            AddList(distributor.ExperienceLevelPatterns, experiencePatterns);
            AddList(distributor.Perks, perks);
            AddList(distributor.PerkGroups, perkGroups);
        }

        private List<T> Parse<T>(ConfigSnapshotFile snapshot, string domainName)
            where T : class
        {
            var domain = FindDomain(snapshot, domainName);
            var rowsJson = domain.Rows.ToString(Formatting.None);
            var result = JsonConvert.DeserializeObject<List<T>>(rowsJson, _rowsSettings);

            if (result == null)
                return new List<T>();

            return result;
        }

        private ConfigSnapshotDomain FindDomain(ConfigSnapshotFile snapshot, string domainName)
        {
            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (string.Equals(domain.Domain, domainName, StringComparison.Ordinal))
                    return domain;
            }

            throw new InvalidOperationException($"[Golden] config snapshot domain missing domain = {domainName}");
        }

        private void AddBonuses(IConfigDistributor distributor, List<BonusMapper> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                _bonusWorkModeParser.Parse(item.WorkModeParameters);
                distributor.Bonuses.Add(item.Id, item);
            }
        }

        private void AddList<TMapper, TInterface>(IManager<TInterface> manager, List<TMapper> items)
            where TMapper : class, TInterface
            where TInterface : class
        {
            for (int i = 0; i < items.Count; i++)
                manager.Add(items[i]);
        }
    }
}

using Core.Collections;
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
    internal sealed class GameConfigSetBuilder
    {
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly ConfigRowsParser _configRowsParser;
        private readonly ConfigSnapshotValidator _configSnapshotValidator;
        private readonly ILogger<GameConfigSetBuilder> _logger;

        public GameConfigSetBuilder(
            IBonusWorkModeParser bonusWorkModeParser,
            ConfigRowsParser configRowsParser,
            ConfigSnapshotValidator configSnapshotValidator,
            ILogger<GameConfigSetBuilder> logger)
        {
            _bonusWorkModeParser = bonusWorkModeParser;
            _configRowsParser = configRowsParser;
            _configSnapshotValidator = configSnapshotValidator;
            _logger = logger;
        }

        public GameConfigBuildResult Build(GameConfigSnapshot snapshot, string source)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            _configSnapshotValidator.ValidateStructure(snapshot, errors, warnings);

            if (0 < errors.Count)
                return new GameConfigBuildResult(null, errors, warnings);

            ConfigDistributor distributor;

            try
            {
                distributor = CreateDistributor(snapshot);
            }
            catch (Exception exception)
            {
                errors.Add($"Config snapshot parse failed: {exception.Message}");

                return new GameConfigBuildResult(null, errors, warnings);
            }

            _configSnapshotValidator.ValidateContent(distributor, errors, warnings);

            if (0 < errors.Count)
                return new GameConfigBuildResult(null, errors, warnings);

            var configSet = new GameConfigSet(snapshot.Version, DateTime.UtcNow, source, distributor);

            return new GameConfigBuildResult(configSet, errors, warnings);
        }

        private ConfigDistributor CreateDistributor(GameConfigSnapshot snapshot)
        {
            var tempConstants = Parse<ConstantsMapper>(snapshot, ConfigDomainNames.Constants);
            var tempCharacters = Parse<CharacterMapper>(snapshot, ConfigDomainNames.Characters);
            var tempBonuses = Parse<BonusMapper>(snapshot, ConfigDomainNames.Bonuses);
            var tempStatuses = Parse<StatusMapper>(snapshot, ConfigDomainNames.Statuses);
            var tempSummons = Parse<SummonMapper>(snapshot, ConfigDomainNames.Summons);
            var tempSummonLevels = Parse<SummonLevelMapper>(snapshot, ConfigDomainNames.SummonLevels);
            var tempMasteries = Parse<MasteryMapper>(snapshot, ConfigDomainNames.Mastery);
            var tempEnemies = Parse<EnemyMapper>(snapshot, ConfigDomainNames.Enemies);
            var tempEquipments = Parse<EquipmentMapper>(snapshot, ConfigDomainNames.Equipments);
            var tempStoryLevels = Parse<StoryLevelMapper>(snapshot, ConfigDomainNames.StoryLevels);
            var tempStoryStages = Parse<StoryStageMapper>(snapshot, ConfigDomainNames.StoryStages);
            var tempStoryEvents = Parse<StoryEventMapper>(snapshot, ConfigDomainNames.StoryEvents);
            var tempExpPatterns = Parse<ExperienceLevelPatternMapper>(snapshot, ConfigDomainNames.ExpLevelsPatterns);
            var tempPerks = Parse<PerkMapper>(snapshot, ConfigDomainNames.Perks);
            var tempPerkGroups = Parse<PerkGroupMapper>(snapshot, ConfigDomainNames.PerkGroups);
            var tempSkills = Parse<SkillMapper>(snapshot, ConfigDomainNames.Skills);

            var distributor = new ConfigDistributor();

            AddList(distributor.Constants, tempConstants, ConfigDomainNames.Constants);
            AddList(distributor.Characters, tempCharacters, ConfigDomainNames.Characters);
            AddBonuses(distributor, tempBonuses);
            AddList(distributor.Statuses, tempStatuses, ConfigDomainNames.Statuses);
            AddList(distributor.Summons, tempSummons, ConfigDomainNames.Summons);
            AddList(distributor.SummonLevels, tempSummonLevels, ConfigDomainNames.SummonLevels);
            AddList(distributor.Masteries, tempMasteries, ConfigDomainNames.Mastery);
            AddList(distributor.Enemies, tempEnemies, ConfigDomainNames.Enemies);
            AddList(distributor.Equipments, tempEquipments, ConfigDomainNames.Equipments);
            AddList(distributor.StoryLevels, tempStoryLevels, ConfigDomainNames.StoryLevels);
            AddList(distributor.StoryStages, tempStoryStages, ConfigDomainNames.StoryStages);
            AddList(distributor.StoryEvents, tempStoryEvents, ConfigDomainNames.StoryEvents);
            AddList(distributor.ExperienceLevelPatterns, tempExpPatterns, ConfigDomainNames.ExpLevelsPatterns);
            AddList(distributor.Perks, tempPerks, ConfigDomainNames.Perks);
            AddList(distributor.PerkGroups, tempPerkGroups, ConfigDomainNames.PerkGroups);
            AddList(distributor.Skills, tempSkills, ConfigDomainNames.Skills);

            _logger.LogInformation($"[Config] Trainings stub empty; sheet id not wired count = {distributor.Trainings.Collection.Count}");
            _logger.LogInformation($"[Config] Artifacts stub empty; sheet id not wired count = {distributor.Artifacts.Collection.Count}");
            _logger.LogInformation($"[Config] Aspects stub empty; sheet id not wired count = {distributor.Aspects.Collection.Count}");
            _logger.LogInformation($"[Config] inventory summary bonuses = {distributor.Bonuses.Count}, statuses = {distributor.Statuses.Collection.Count}, perks = {distributor.Perks.Collection.Count}, perkGroups = {distributor.PerkGroups.Collection.Count}, skills = {distributor.Skills.Collection.Count}, trainings = {distributor.Trainings.Collection.Count}, artifacts = {distributor.Artifacts.Collection.Count}, aspects = {distributor.Aspects.Collection.Count}");

            return distributor;
        }

        private List<T> Parse<T>(GameConfigSnapshot snapshot, string domainName)
            where T : class
        {
            snapshot.TryGetDomain(domainName, out var domain);

            return _configRowsParser.Parse<T>(domain.RowsJson);
        }

        private void AddBonuses(ConfigDistributor distributor, List<BonusMapper> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (_bonusWorkModeParser.TryParse(item.WorkModeParameters, out var workMode) == false)
                    _logger.LogError($"[Config] bonus work_mode parse failed id = {item.Id}");

                if (distributor.Bonuses.Add(item.Id, item) == false)
                {
                    _logger.LogWarning($"[Config] duplicate bonus id = {item.Id}; skipped");

                    continue;
                }

                _logger.LogDebug($"[Config] bonus loaded id = {item.Id} type = {item.BonusType} workMode = {workMode.Format()}");
            }

            _logger.LogInformation($"[Config] Bonuses count = {distributor.Bonuses.Count}");
        }

        private void AddList<TMapper, TInterface>(
            IManager<TInterface> manager,
            List<TMapper> items,
            string sheetName)
            where TMapper : class, TInterface
            where TInterface : class
        {
            for (int i = 0; i < items.Count; i++)
                manager.Add(items[i]);

            _logger.LogInformation($"[Config] {sheetName} count = {manager.Collection.Count}");
        }
    }
}

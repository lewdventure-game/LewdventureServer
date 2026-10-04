using Server.Collections;
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
        private readonly ICoreLog _coreLog;

        public GameConfigSetBuilder(
            IBonusWorkModeParser bonusWorkModeParser,
            ConfigRowsParser configRowsParser,
            ConfigSnapshotValidator configSnapshotValidator,
            ICoreLog coreLog)
        {
            _bonusWorkModeParser = bonusWorkModeParser;
            _configRowsParser = configRowsParser;
            _configSnapshotValidator = configSnapshotValidator;
            _coreLog = coreLog;
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
                distributor = CreateDistributor(snapshot, errors);
            }
            catch (Exception exception)
            {
                errors.Add($"Config snapshot parse failed: {exception.Message}");

                return new GameConfigBuildResult(null, errors, warnings);
            }

            if (0 < errors.Count)
                return new GameConfigBuildResult(null, errors, warnings);

            _configSnapshotValidator.ValidateContent(distributor, errors, warnings);

            if (0 < errors.Count)
                return new GameConfigBuildResult(null, errors, warnings);

            var configSet = new GameConfigSet(snapshot.Version, DateTime.UtcNow, source, distributor, snapshot);

            return new GameConfigBuildResult(configSet, errors, warnings);
        }

        private ConfigDistributor CreateDistributor(GameConfigSnapshot snapshot, List<string> errors)
        {
            var tempConstants = ParseDomain<ConstantsMapper>(snapshot, ConfigDomainNames.Constants, errors);
            var tempCharacters = ParseDomain<CharacterMapper>(snapshot, ConfigDomainNames.Characters, errors);
            var tempCharacterPromotes = ParseDomain<CharacterPromoteMapper>(snapshot, ConfigDomainNames.CharacterPromotes, errors);
            var tempBonuses = ParseDomain<BonusMapper>(snapshot, ConfigDomainNames.Bonuses, errors);
            var tempStatuses = ParseDomain<StatusMapper>(snapshot, ConfigDomainNames.Statuses, errors);
            var tempSummons = ParseDomain<SummonMapper>(snapshot, ConfigDomainNames.Summons, errors);
            var tempSummonLevels = ParseDomain<SummonLevelMapper>(snapshot, ConfigDomainNames.SummonLevels, errors);
            var tempSummonMasteries = ParseDomain<SummonMasteryMapper>(snapshot, ConfigDomainNames.SummonMasteries, errors);
            var tempEnemies = ParseDomain<EnemyMapper>(snapshot, ConfigDomainNames.Enemies, errors);
            var tempEquipments = ParseDomain<EquipmentMapper>(snapshot, ConfigDomainNames.Equipments, errors);
            var tempEquipmentPromotes = ParseDomain<EquipmentPromoteMapper>(snapshot, ConfigDomainNames.EquipmentPromotes, errors);
            var tempStoryLevels = ParseDomain<StoryLevelMapper>(snapshot, ConfigDomainNames.StoryLevels, errors);
            var tempStoryStages = ParseDomain<StoryStageMapper>(snapshot, ConfigDomainNames.StoryStages, errors);
            var tempStoryEvents = ParseDomain<StoryEventMapper>(snapshot, ConfigDomainNames.StoryEvents, errors);
            var tempExpPatterns = ParseDomain<ExperienceLevelPatternMapper>(snapshot, ConfigDomainNames.ExpLevelsPatterns, errors);
            var tempPerks = ParseDomain<PerkMapper>(snapshot, ConfigDomainNames.Perks, errors);
            var tempPerkGroups = ParseDomain<PerkGroupMapper>(snapshot, ConfigDomainNames.PerkGroups, errors);
            var tempSkills = ParseDomain<SkillMapper>(snapshot, ConfigDomainNames.Skills, errors);
            var tempSkillPromotes = ParseDomain<SkillPromoteMapper>(snapshot, ConfigDomainNames.SkillPromotes, errors);

            var distributor = new ConfigDistributor();

            AddList(distributor.Constants, tempConstants, ConfigDomainNames.Constants);
            AddList(distributor.Characters, tempCharacters, ConfigDomainNames.Characters);
            AddList(distributor.CharacterPromotes, tempCharacterPromotes, ConfigDomainNames.CharacterPromotes);
            AddBonuses(distributor, tempBonuses);
            AddList(distributor.Statuses, tempStatuses, ConfigDomainNames.Statuses);
            AddList(distributor.Summons, tempSummons, ConfigDomainNames.Summons);
            AddList(distributor.SummonLevels, tempSummonLevels, ConfigDomainNames.SummonLevels);
            AddList(distributor.SummonMasteries, tempSummonMasteries, ConfigDomainNames.SummonMasteries);
            AddList(distributor.Enemies, tempEnemies, ConfigDomainNames.Enemies);
            AddList(distributor.Equipments, tempEquipments, ConfigDomainNames.Equipments);
            AddList(distributor.EquipmentPromotes, tempEquipmentPromotes, ConfigDomainNames.EquipmentPromotes);
            AddList(distributor.StoryLevels, tempStoryLevels, ConfigDomainNames.StoryLevels);
            AddList(distributor.StoryStages, tempStoryStages, ConfigDomainNames.StoryStages);
            AddList(distributor.StoryEvents, tempStoryEvents, ConfigDomainNames.StoryEvents);
            AddList(distributor.ExperienceLevelPatterns, tempExpPatterns, ConfigDomainNames.ExpLevelsPatterns);
            AddList(distributor.Perks, tempPerks, ConfigDomainNames.Perks);
            AddList(distributor.PerkGroups, tempPerkGroups, ConfigDomainNames.PerkGroups);
            AddList(distributor.Skills, tempSkills, ConfigDomainNames.Skills);
            AddList(distributor.SkillPromotes, tempSkillPromotes, ConfigDomainNames.SkillPromotes);

            if (distributor.CharacterPromotes.Collection.Count == 0)
                _coreLog.Information("[Config] Character_promotes empty; sheet id not wired");

            _coreLog.Information($"[Config] Trainings stub empty; sheet id not wired count = {distributor.Trainings.Collection.Count}");
            _coreLog.Information($"[Config] Artifacts stub empty; sheet id not wired count = {distributor.Artifacts.Collection.Count}");
            _coreLog.Information($"[Config] Aspects stub empty; sheet id not wired count = {distributor.Aspects.Collection.Count}");
            _coreLog.Information($"[Config] inventory summary bonuses = {distributor.Bonuses.Count}, statuses = {distributor.Statuses.Collection.Count}, perks = {distributor.Perks.Collection.Count}, perkGroups = {distributor.PerkGroups.Collection.Count}, skills = {distributor.Skills.Collection.Count}, trainings = {distributor.Trainings.Collection.Count}, artifacts = {distributor.Artifacts.Collection.Count}, aspects = {distributor.Aspects.Collection.Count}");

            return distributor;
        }

        private List<T> ParseDomain<T>(GameConfigSnapshot snapshot, string domainName, List<string> errors)
            where T : class
        {
            if (snapshot.TryGetDomain(domainName, out var domain) == false)
                return new List<T>();

            return _configRowsParser.Parse<T>(domain, errors);
        }

        private void AddBonuses(ConfigDistributor distributor, List<BonusMapper> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];

                if (_bonusWorkModeParser.TryParse(item.WorkModeParameters, out var workMode) == false)
                    _coreLog.Error($"[Config] bonus work_mode parse failed id = {item.Id}");

                if (distributor.Bonuses.Add(item.Id, item) == false)
                {
                    _coreLog.Warning($"[Config] duplicate bonus id = {item.Id}; skipped");

                    continue;
                }

                _coreLog.Debug($"[Config] bonus loaded id = {item.Id} type = {item.BonusType} workMode = {workMode.Format()}");
            }

            _coreLog.Information($"[Config] Bonuses count = {distributor.Bonuses.Count}");
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

            _coreLog.Information($"[Config] {sheetName} count = {manager.Collection.Count}");
        }
    }
}

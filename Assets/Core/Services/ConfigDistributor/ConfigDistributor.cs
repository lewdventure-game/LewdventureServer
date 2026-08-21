using Server.Artifacts;
using Server.Aspects;
using Server.Bonuses;
using Server.Common;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Perks;
using Server.Statuses;
using Server.Stories;
using Server.Trainings;

namespace Server.Services
{
    internal sealed class ConfigDistributor : IConfigDistributor
    {
        public IConstantsMapperManager Constants { get; } = new ConstantsMapperManager();

        public IArtifactMapperManager Artifacts { get; } = new ArtifactMapperManager();

        public IAspectMapperManager Aspects { get; } = new AspectMapperManager();

        public IBonusMapperManager Bonuses { get; } = new BonusMapperManager();

        public ICharacterMapperManager Characters { get; } = new CharacterMapperManager();

        public IEnemyMapperManager Enemies { get; } = new EnemyMapperManager();

        public IEquipmentMapperManager Equipments { get; } = new EquipmentMapperManager();

        public IExperienceLevelPatternMapperManager ExperienceLevelPatterns { get; } = new ExperienceLevelPatternMapperManager();

        public IMasteryMapperManager Masteries { get; } = new MasteryMapperManager();

        public IPerkGroupMapperManager PerkGroups { get; } = new PerkGroupMapperManager();

        public IPerkMapperManager Perks { get; } = new PerkMapperManager();

        public IStatusMapperManager Statuses { get; } = new StatusMapperManager();

        public IStoryEventMapperManager StoryEvents { get; } = new StoryEventMapperManager();

        public IStoryLevelMapperManager StoryLevels { get; } = new StoryLevelMapperManager();

        public IStoryStageMapperManager StoryStages { get; } = new StoryStageMapperManager();

        public ISummonLevelMapperManager SummonLevels { get; } = new SummonLevelMapperManager();

        public ISummonMapperManager Summons { get; } = new SummonMapperManager();

        public ITrainingMapperManager Trainings { get; } = new TrainingMapperManager();

        public void ClearAll()
        {
            Constants.Clear();
            Artifacts.Clear();
            Aspects.Clear();
            Bonuses.Clear();
            Characters.Clear();
            Enemies.Clear();
            Equipments.Clear();
            ExperienceLevelPatterns.Clear();
            Masteries.Clear();
            PerkGroups.Clear();
            Perks.Clear();
            Statuses.Clear();
            StoryEvents.Clear();
            StoryLevels.Clear();
            StoryStages.Clear();
            SummonLevels.Clear();
            Summons.Clear();
            Trainings.Clear();
        }
    }
}

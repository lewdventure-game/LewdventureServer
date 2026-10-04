using Server.Artifacts;
using Server.Aspects;
using Server.Bonuses;
using Server.Common;
using Server.Configs;
using Server.Entities;
using Server.Equipments;
using Server.Perks;
using Server.Skills;
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

        public ICharacterPromoteMapperManager CharacterPromotes { get; } = new CharacterPromoteMapperManager();

        public IEnemyMapperManager Enemies { get; } = new EnemyMapperManager();

        public IEquipmentMapperManager Equipments { get; } = new EquipmentMapperManager();

        public IEquipmentPromoteMapperManager EquipmentPromotes { get; } = new EquipmentPromoteMapperManager();

        public IExperienceLevelPatternMapperManager ExperienceLevelPatterns { get; } = new ExperienceLevelPatternMapperManager();

        public IPerkGroupMapperManager PerkGroups { get; } = new PerkGroupMapperManager();

        public IPerkMapperManager Perks { get; } = new PerkMapperManager();

        public ISkillMapperManager Skills { get; } = new SkillMapperManager();

        public ISkillPromoteMapperManager SkillPromotes { get; } = new SkillPromoteMapperManager();

        public IStatusMapperManager Statuses { get; } = new StatusMapperManager();

        public IStoryEventMapperManager StoryEvents { get; } = new StoryEventMapperManager();

        public IStoryLevelMapperManager StoryLevels { get; } = new StoryLevelMapperManager();

        public IStoryStageMapperManager StoryStages { get; } = new StoryStageMapperManager();

        public ISummonLevelMapperManager SummonLevels { get; } = new SummonLevelMapperManager();

        public ISummonMasteryMapperManager SummonMasteries { get; } = new SummonMasteryMapperManager();

        public ISummonMapperManager Summons { get; } = new SummonMapperManager();

        public ITrainingMapperManager Trainings { get; } = new TrainingMapperManager();
    }
}

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
    internal interface IConfigDistributor
    {
        public IConstantsMapperManager Constants { get; }

        public IArtifactMapperManager Artifacts { get; }

        public IAspectMapperManager Aspects { get; }

        public IBonusMapperManager Bonuses { get; }

        public ICharacterMapperManager Characters { get; }

        public IEnemyMapperManager Enemies { get; }

        public IEquipmentMapperManager Equipments { get; }

        public IExperienceLevelPatternMapperManager ExperienceLevelPatterns { get; }

        public IMasteryMapperManager Masteries { get; }

        public IPerkGroupMapperManager PerkGroups { get; }

        public IPerkMapperManager Perks { get; }

        public ISkillMapperManager Skills { get; }

        public IStatusMapperManager Statuses { get; }

        public IStoryEventMapperManager StoryEvents { get; }

        public IStoryLevelMapperManager StoryLevels { get; }

        public IStoryStageMapperManager StoryStages { get; }

        public ISummonLevelMapperManager SummonLevels { get; }

        public ISummonMapperManager Summons { get; }

        public ITrainingMapperManager Trainings { get; }
    }
}

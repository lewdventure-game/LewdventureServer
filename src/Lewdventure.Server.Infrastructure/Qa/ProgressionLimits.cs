using Server.Entities;
using Server.Equipments;
using Server.Services;

namespace Server.Infrastructure.Qa
{
    internal sealed class ProgressionLimits
    {
        private const int MaxScannedLevel = 1000;

        public int GetMaxPromoteLevel(ICharacterMapper character, IConfigDistributor configDistributor)
        {
            if (character.PromoteId <= 0)
                return 1;

            var maxLevel = configDistributor.CharacterPromotes.GetMaxPromoteLevel(character.PromoteId);

            return maxLevel < 1 ? 1 : maxLevel;
        }

        public int GetMaxSummonLevel(ISummonMapper summon, IConfigDistributor configDistributor)
        {
            var level = 1;

            while (level < MaxScannedLevel && configDistributor.SummonLevels.TryGet(summon.LevelPatternId, level + 1, out _))
                level++;

            return level;
        }

        public int GetMaxMasteryLevel(ISummonMapper summon, IConfigDistributor configDistributor)
        {
            var maxLevel = configDistributor.SummonMasteries.GetMaxMasteryLevel(summon.MasteryId);

            return maxLevel < 1 ? 1 : maxLevel;
        }

        public int GetMaxSkillLevel(ISummonMapper summon, int skillIndex, IConfigDistributor configDistributor)
        {
            var patternId = skillIndex < summon.SkillUpgradeIds.Length ? summon.SkillUpgradeIds[skillIndex] : 0;

            if (patternId <= 0)
                return 1;

            var level = 1;

            while (level < MaxScannedLevel && configDistributor.SkillPromotes.TryGet(patternId, level + 1, out _))
                level++;

            return level;
        }

        public int GetMaxEquipmentLevel(IEquipmentMapper equipment, IConfigDistributor configDistributor)
        {
            if (equipment.PromoteId <= 0)
                return 1;

            var maxLevel = configDistributor.EquipmentPromotes.GetMaxLevel(equipment.PromoteId);

            return maxLevel < 1 ? 1 : maxLevel;
        }
    }
}

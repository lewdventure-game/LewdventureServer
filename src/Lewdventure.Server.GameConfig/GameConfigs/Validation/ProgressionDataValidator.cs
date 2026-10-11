using Server.Services;

namespace Server.GameConfigs
{
    internal sealed class ProgressionDataValidator
    {
        public void Collect(IConfigDistributor distributor, List<string> warnings)
        {
            CollectSummonDamageLevels(distributor, warnings);
            CollectEquipmentBonusLevels(distributor, warnings);
        }

        private void CollectSummonDamageLevels(IConfigDistributor distributor, List<string> warnings)
        {
            var summons = distributor.Summons.Collection;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];
                var maxLevel = ReadMaxSummonLevel(distributor, summon.LevelPatternId);

                if (maxLevel <= summon.DamageOnLevels.Length)
                    continue;

                warnings.Add($"Лист Summons, саммон {summon.Id}: в dmg_on_lvls {summon.DamageOnLevels.Length} значений, а уровней в Summon_levels до {maxLevel}. На уровнях выше {summon.DamageOnLevels.Length} урон перестанет расти.");
            }
        }

        private int ReadMaxSummonLevel(IConfigDistributor distributor, int patternId)
        {
            var levels = distributor.SummonLevels.Collection;
            var maxLevel = 1;

            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].PatternId == patternId && maxLevel < levels[i].Level)
                    maxLevel = levels[i].Level;
            }

            return maxLevel;
        }

        private void CollectEquipmentBonusLevels(IConfigDistributor distributor, List<string> warnings)
        {
            var equipments = distributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var equipment = equipments[i];

                if (equipment.PromoteId <= 0)
                    continue;

                var maxLevel = distributor.EquipmentPromotes.GetMaxLevel(equipment.PromoteId);

                for (int bonusIndex = 0; bonusIndex < equipment.BonusIds.Length; bonusIndex++)
                {
                    var bonusId = equipment.BonusIds[bonusIndex];

                    if (distributor.Bonuses.TryGet(bonusId, out var bonus) == false)
                        continue;

                    var valuesCount = bonus.BonusValues.Length;

                    if (valuesCount <= 1 || maxLevel <= valuesCount)
                        continue;

                    warnings.Add($"Лист Bonuses, бонус {bonusId} снаряжения {equipment.Id}: значений {valuesCount}, а уровней снаряжения до {maxLevel}. На уровнях выше {valuesCount} бонус перестанет расти.");
                }
            }
        }
    }
}

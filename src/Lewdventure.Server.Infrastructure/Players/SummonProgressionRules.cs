using Server.Entities;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class SummonProgressionRules
    {
        private const string ResourceType = "resource";

        public bool TryResolveLevelStep(
            ISummonMapper summon,
            int currentLevel,
            IConfigDistributor configDistributor,
            out List<ResourceCost> costs,
            out string error)
        {
            costs = new List<ResourceCost>();
            error = string.Empty;

            var targetLevel = currentLevel + 1;

            if (configDistributor.SummonLevels.TryGet(summon.LevelPatternId, targetLevel, out var step) == false)
            {
                error = $"Summon {summon.Id} has no level {targetLevel} in pattern {summon.LevelPatternId}.";

                return false;
            }

            if (TryCollectCosts(step.ResourceTypes, step.ResourceIds, step.ResourceValues, costs, out error) == false)
                return false;

            if (costs.Count == 0)
            {
                error = $"Summon {summon.Id} level {targetLevel} has no cost configured.";

                return false;
            }

            return true;
        }

        public bool TryResolveLevelRefund(
            ISummonMapper summon,
            int currentLevel,
            float resetCoefficient,
            IConfigDistributor configDistributor,
            out List<ResourceCost> refunds,
            out string error)
        {
            refunds = new List<ResourceCost>();
            error = string.Empty;

            if (currentLevel <= 1)
            {
                error = $"Summon {summon.Id} is already at level 1.";

                return false;
            }

            var spent = new Dictionary<string, long>(StringComparer.Ordinal);
            var stepCosts = new List<ResourceCost>();

            for (int level = 2; level <= currentLevel; level++)
            {
                if (configDistributor.SummonLevels.TryGet(summon.LevelPatternId, level, out var step) == false)
                {
                    error = $"Summon {summon.Id} has no level {level} in pattern {summon.LevelPatternId}.";

                    return false;
                }

                stepCosts.Clear();

                if (TryCollectCosts(step.ResourceTypes, step.ResourceIds, step.ResourceValues, stepCosts, out error) == false)
                    return false;

                for (int i = 0; i < stepCosts.Count; i++)
                {
                    spent.TryGetValue(stepCosts[i].Key, out var current);
                    spent[stepCosts[i].Key] = current + stepCosts[i].Amount;
                }
            }

            foreach (var pair in spent)
            {
                var refund = (int)MathF.Round(pair.Value * resetCoefficient, MidpointRounding.AwayFromZero);

                if (refund <= 0)
                    continue;

                refunds.Add(new ResourceCost(pair.Key, refund));
            }

            return true;
        }

        public bool TryResolveMasteryStep(
            ISummonMapper summon,
            int currentMasteryLevel,
            IConfigDistributor configDistributor,
            out int copies,
            out string error)
        {
            copies = 0;
            error = string.Empty;

            var targetLevel = currentMasteryLevel + 1;

            if (configDistributor.SummonMasteries.TryGet(summon.MasteryId, targetLevel, out var mastery) == false)
            {
                error = $"Summon {summon.Id} has no mastery level {targetLevel}.";

                return false;
            }

            if (mastery.CopiesToUpgrade <= 0)
            {
                error = $"Summon mastery {summon.MasteryId} level {targetLevel} has no copy cost.";

                return false;
            }

            copies = mastery.CopiesToUpgrade;

            return true;
        }

        public bool TryResolveSkillIndex(ISummonMapper summon, int skillId, out int skillIndex, out string error)
        {
            error = string.Empty;

            var skillKey = skillId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var skillIds = summon.SkillIds;

            for (int i = 0; i < skillIds.Length; i++)
            {
                if (string.Equals(skillIds[i].Trim(), skillKey, StringComparison.Ordinal) == false)
                    continue;

                skillIndex = i;

                return true;
            }

            skillIndex = -1;
            error = $"Summon {summon.Id} has no skill {skillId}.";

            return false;
        }

        public bool TryResolveSkillStep(
            ISummonMapper summon,
            int skillIndex,
            int currentSkillLevel,
            int summonLevel,
            int masteryLevel,
            IConfigDistributor configDistributor,
            out List<ResourceCost> costs,
            out string error)
        {
            costs = new List<ResourceCost>();
            error = string.Empty;

            var skillId = summon.SkillIds[skillIndex];
            var requiredMastery = skillIndex < summon.MasteryForSkills.Length ? summon.MasteryForSkills[skillIndex] : 0;

            if (masteryLevel < requiredMastery)
            {
                error = $"Summon {summon.Id} skill {skillId} needs mastery {requiredMastery}, has {masteryLevel}.";

                return false;
            }

            var patternId = skillIndex < summon.SkillUpgradeIds.Length ? summon.SkillUpgradeIds[skillIndex] : 0;

            if (patternId <= 0)
            {
                error = $"Summon {summon.Id} skill {skillId} has no skill_upgrade_ids pattern.";

                return false;
            }

            var targetLevel = currentSkillLevel + 1;

            if (configDistributor.SkillPromotes.TryGet(patternId, targetLevel, out var step) == false)
            {
                error = $"Summon {summon.Id} skill {skillId} has no level {targetLevel} in pattern {patternId}.";

                return false;
            }

            if (summonLevel < step.LevelToUnlock)
            {
                error = $"Summon {summon.Id} skill {skillId} level {targetLevel} needs summon level {step.LevelToUnlock}, has {summonLevel}.";

                return false;
            }

            if (TryCollectCosts(step.ResourceTypes, step.ResourceIds, step.ResourceValues, costs, out error) == false)
                return false;

            if (costs.Count == 0)
            {
                error = $"Summon {summon.Id} skill {skillId} level {targetLevel} has no cost configured.";

                return false;
            }

            return true;
        }

        private bool TryCollectCosts(string[] types, string[] ids, int[] values, List<ResourceCost> costs, out string error)
        {
            error = string.Empty;

            for (int i = 0; i < ids.Length; i++)
            {
                var type = types.Length <= i ? ResourceType : types[i];

                if (string.Equals(type, ResourceType, StringComparison.OrdinalIgnoreCase) == false)
                {
                    error = $"Cost type {type} is not supported, only {ResourceType}.";

                    return false;
                }

                var value = values.Length <= i ? 0 : values[i];

                if (value <= 0)
                    continue;

                costs.Add(new ResourceCost(ids[i], value));
            }

            return true;
        }
    }
}

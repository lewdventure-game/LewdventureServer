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
            int currentMasteryLevel,
            IConfigDistributor configDistributor,
            out List<ResourceCost> costs,
            out string error)
        {
            costs = new List<ResourceCost>();
            error = string.Empty;

            if (configDistributor.SummonLevels.TryGet(summon.LevelUpgradePattern, currentLevel, out var step) == false)
            {
                error = $"Summon {summon.Id} has no level {currentLevel + 1} in pattern {summon.LevelUpgradePattern}.";

                return false;
            }

            if (currentMasteryLevel < step.MasteryRequirement)
            {
                error = $"Summon {summon.Id} needs mastery {step.MasteryRequirement} for level {currentLevel + 1}.";

                return false;
            }

            for (int i = 0; i < step.ResourceIds.Length; i++)
            {
                var type = step.ResourceTypes.Length <= i ? ResourceType : step.ResourceTypes[i];

                if (string.Equals(type, ResourceType, StringComparison.OrdinalIgnoreCase) == false)
                {
                    error = $"Summon level cost type {type} is not supported.";

                    return false;
                }

                var value = step.ResourceValues.Length <= i ? 0 : step.ResourceValues[i];

                if (value <= 0)
                    continue;

                costs.Add(new ResourceCost(step.ResourceIds[i], value));
            }

            if (costs.Count == 0)
            {
                error = $"Summon {summon.Id} level {currentLevel + 1} has no cost configured.";

                return false;
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

            if (configDistributor.Masteries.TryGet(summon.MasteryId, currentMasteryLevel + 1, out var mastery) == false)
            {
                error = $"Summon {summon.Id} has no mastery level {currentMasteryLevel + 1}.";

                return false;
            }

            if (mastery.CopiesToUpgrade <= 0)
            {
                error = $"Mastery {summon.MasteryId} level {currentMasteryLevel + 1} has no copy cost.";

                return false;
            }

            copies = mastery.CopiesToUpgrade;

            return true;
        }
    }
}

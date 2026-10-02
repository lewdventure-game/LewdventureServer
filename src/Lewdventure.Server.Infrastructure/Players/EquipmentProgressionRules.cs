using Server.Equipments;

namespace Server.Infrastructure.Players
{
    internal sealed class EquipmentProgressionRules
    {
        private const string ResourceType = "resource";

        public bool TryResolveLevelStep(
            IEquipmentMapper equipment,
            int currentLevel,
            IEquipmentPromoteMapperManager promotes,
            out List<ResourceCost> costs,
            out string error)
        {
            costs = new List<ResourceCost>();
            error = string.Empty;

            if (equipment.PromoteId <= 0)
            {
                error = $"Equipment {equipment.Id} has no promote_id.";

                return false;
            }

            var nextLevel = currentLevel + 1;
            var maxLevel = promotes.GetMaxLevel(equipment.PromoteId);

            if (maxLevel < nextLevel)
            {
                error = $"Equipment {equipment.Id} is already at max level {maxLevel}.";

                return false;
            }

            if (promotes.TryGet(equipment.PromoteId, nextLevel, out var promote) == false)
            {
                error = $"Equipment {equipment.Id} has no cost for level {nextLevel}.";

                return false;
            }

            return TryCollectCosts(equipment, promote, costs, out error);
        }

        public bool TryResolveLevelRefund(
            IEquipmentMapper equipment,
            int currentLevel,
            float dropProportion,
            IEquipmentPromoteMapperManager promotes,
            out List<ResourceCost> refunds,
            out string error)
        {
            refunds = new List<ResourceCost>();
            error = string.Empty;

            if (currentLevel <= 1)
            {
                error = $"Equipment {equipment.Id} is already at level 1.";

                return false;
            }

            if (equipment.PromoteId <= 0)
            {
                error = $"Equipment {equipment.Id} has no promote_id.";

                return false;
            }

            var spent = new Dictionary<string, long>(StringComparer.Ordinal);

            for (int level = 2; level <= currentLevel; level++)
            {
                if (promotes.TryGet(equipment.PromoteId, level, out var promote) == false)
                {
                    error = $"Equipment {equipment.Id} has no cost for level {level}.";

                    return false;
                }

                var costs = new List<ResourceCost>();

                if (TryCollectCosts(equipment, promote, costs, out error) == false)
                    return false;

                for (int i = 0; i < costs.Count; i++)
                {
                    spent.TryGetValue(costs[i].Key, out var current);
                    spent[costs[i].Key] = current + costs[i].Amount;
                }
            }

            foreach (var pair in spent)
            {
                var refund = (int)MathF.Round(pair.Value * dropProportion, MidpointRounding.AwayFromZero);

                if (refund <= 0)
                    continue;

                refunds.Add(new ResourceCost(pair.Key, refund));
            }

            return true;
        }

        private bool TryCollectCosts(IEquipmentMapper equipment, IEquipmentPromoteMapper promote, List<ResourceCost> costs, out string error)
        {
            error = string.Empty;

            var types = promote.ResourceTypes;
            var ids = promote.ResourceIds;
            var values = promote.ResourceValues;

            if (types.Length == 0)
            {
                error = $"Equipment {equipment.Id} level {promote.Level} has no cost configured.";

                return false;
            }

            if (types.Length != ids.Length || types.Length != values.Length)
            {
                error = $"Equipment {equipment.Id} level {promote.Level} has mismatched cost columns.";

                return false;
            }

            for (int i = 0; i < types.Length; i++)
            {
                if (string.Equals(types[i].Trim(), ResourceType, StringComparison.OrdinalIgnoreCase) == false)
                {
                    error = $"Equipment {equipment.Id} level {promote.Level} has an unsupported cost type {types[i]}.";

                    return false;
                }

                var resourceId = ids[i].Trim();

                if (resourceId.Length == 0)
                {
                    error = $"Equipment {equipment.Id} level {promote.Level} has an empty cost resource.";

                    return false;
                }

                if (values[i] <= 0)
                {
                    error = $"Equipment {equipment.Id} level {promote.Level} has an invalid cost {values[i]}.";

                    return false;
                }

                costs.Add(new ResourceCost(resourceId, values[i]));
            }

            return true;
        }
    }
}

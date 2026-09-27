using System.Globalization;
using Server.Equipments;

namespace Server.Infrastructure.Players
{
    internal sealed class EquipmentProgressionRules
    {
        private const char ListSeparator = ';';
        private const char TypeSeparator = ':';
        private const string ResourcePrefix = "resource";

        public bool TryResolveLevelStep(
            IEquipmentMapper equipment,
            int currentLevel,
            out List<ResourceCost> costs,
            out string error)
        {
            costs = new List<ResourceCost>();
            error = string.Empty;

            var types = Split(equipment.LevelUpTypes);
            var values = Split(equipment.LevelUpValues);

            if (types.Count == 0 || values.Count == 0)
            {
                error = $"Equipment {equipment.Id} has no level up costs configured.";

                return false;
            }

            var stepIndex = currentLevel - 1;

            if (stepIndex < 0 || types.Count <= stepIndex)
            {
                error = $"Equipment {equipment.Id} is already at max level {types.Count}.";

                return false;
            }

            if (values.Count <= stepIndex)
            {
                error = $"Equipment {equipment.Id} has no cost value for level {currentLevel + 1}.";

                return false;
            }

            var resourceKey = ResolveResourceKey(types[stepIndex]);

            if (string.IsNullOrEmpty(resourceKey))
            {
                error = $"Equipment {equipment.Id} level {currentLevel + 1} has an unsupported cost type {types[stepIndex]}.";

                return false;
            }

            if (int.TryParse(values[stepIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) == false || amount <= 0)
            {
                error = $"Equipment {equipment.Id} level {currentLevel + 1} has an invalid cost {values[stepIndex]}.";

                return false;
            }

            costs.Add(new ResourceCost(resourceKey, amount));

            return true;
        }

        private string ResolveResourceKey(string rawType)
        {
            var value = rawType.Trim();

            if (value.Length == 0)
                return string.Empty;

            var separator = value.IndexOf(TypeSeparator);

            if (separator < 0)
                return value;

            var prefix = value.Substring(0, separator).Trim();
            var key = value.Substring(separator + 1).Trim();

            if (string.Equals(prefix, ResourcePrefix, StringComparison.OrdinalIgnoreCase) == false)
                return string.Empty;

            return key;
        }

        private List<string> Split(string value)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(value))
                return result;

            var parts = value.Split(ListSeparator, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (0 < part.Length)
                    result.Add(part);
            }

            return result;
        }
    }
}

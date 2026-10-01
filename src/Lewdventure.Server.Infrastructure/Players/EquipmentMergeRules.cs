using System.Globalization;
using Server.Equipments;
using Server.Services;

namespace Server.Infrastructure.Players
{
    internal sealed class EquipmentMergeRules
    {
        private const char PrimarySeparator = ';';
        private const char SecondarySeparator = ',';
        private const char ValueSeparator = ':';
        private const string ConfigIdRequirement = "equipment_id";
        private const string RarityRequirement = "equipment_rarity";

        public bool TryResolveRequirements(IEquipmentMapper source, out List<EquipmentMergeRequirement> requirements, out string error)
        {
            requirements = new List<EquipmentMergeRequirement>();
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(source.MergeRequirements))
            {
                error = $"Equipment {source.Id} cannot be transformed: merge_requirements is empty.";

                return false;
            }

            var parts = source.MergeRequirements.Split(new[] { PrimarySeparator, SecondarySeparator }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                var separator = part.IndexOf(ValueSeparator);

                if (separator <= 0 || separator == part.Length - 1)
                {
                    error = $"Equipment {source.Id} has an unreadable merge requirement {part}.";

                    return false;
                }

                var key = part.Substring(0, separator).Trim();
                var value = part.Substring(separator + 1).Trim();

                if (string.Equals(key, ConfigIdRequirement, StringComparison.OrdinalIgnoreCase))
                {
                    requirements.Add(new EquipmentMergeRequirement(EquipmentMergeRequirementKind.ConfigId, value));

                    continue;
                }

                if (string.Equals(key, RarityRequirement, StringComparison.OrdinalIgnoreCase))
                {
                    requirements.Add(new EquipmentMergeRequirement(EquipmentMergeRequirementKind.Rarity, value));

                    continue;
                }

                error = $"Equipment {source.Id} has an unsupported merge requirement {key}.";

                return false;
            }

            if (requirements.Count == 0)
            {
                error = $"Equipment {source.Id} has no merge requirements.";

                return false;
            }

            return true;
        }

        public bool TryMatchPayment(
            IEquipmentMapper source,
            IReadOnlyList<EquipmentMergeRequirement> requirements,
            IReadOnlyList<EquipmentMergeCandidate> candidates,
            out string error)
        {
            error = string.Empty;

            if (candidates.Count != requirements.Count)
            {
                error = $"Transformation needs {requirements.Count} items, got {candidates.Count}.";

                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i].Kind != EquipmentMergeRequirementKind.ConfigId)
                    continue;

                if (TryUseCandidate(requirements[i], source, candidates) == false)
                {
                    error = $"Transformation needs equipment with id {requirements[i].Value}.";

                    return false;
                }
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i].Kind != EquipmentMergeRequirementKind.Rarity)
                    continue;

                if (TryUseCandidate(requirements[i], source, candidates) == false)
                {
                    error = $"Transformation needs equipment of type {source.Type} with rarity {requirements[i].Value}.";

                    return false;
                }
            }

            return true;
        }

        public bool TryResolveTarget(IEquipmentMapper source, IConfigDistributor configDistributor, out IEquipmentMapper target, out string error)
        {
            target = null!;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(source.MergeGroup))
            {
                error = $"Equipment {source.Id} has no merge_group.";

                return false;
            }

            if (TryParseMergeNumber(source.MergeNumber, out var sourceNumber) == false)
            {
                error = $"Equipment {source.Id} has an unreadable merge_number {source.MergeNumber}.";

                return false;
            }

            var equipments = configDistributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var candidate = equipments[i];

                if (string.Equals(candidate.MergeGroup.Trim(), source.MergeGroup.Trim(), StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                if (TryParseMergeNumber(candidate.MergeNumber, out var candidateNumber) == false)
                    continue;

                if (candidateNumber != sourceNumber + 1)
                    continue;

                target = candidate;

                return true;
            }

            error = $"Merge group {source.MergeGroup} has no equipment with merge_number {sourceNumber + 1}.";

            return false;
        }

        public bool TryParseMergeNumber(string rawValue, out int mergeNumber)
        {
            mergeNumber = 0;

            if (string.IsNullOrWhiteSpace(rawValue))
                return true;

            return int.TryParse(rawValue.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out mergeNumber);
        }

        private bool TryUseCandidate(
            in EquipmentMergeRequirement requirement,
            IEquipmentMapper source,
            IReadOnlyList<EquipmentMergeCandidate> candidates)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];

                if (candidate.IsUsed)
                    continue;

                if (Matches(requirement, source, candidate.Mapper) == false)
                    continue;

                candidate.IsUsed = true;

                return true;
            }

            return false;
        }

        private bool Matches(in EquipmentMergeRequirement requirement, IEquipmentMapper source, IEquipmentMapper candidate)
        {
            if (requirement.Kind == EquipmentMergeRequirementKind.ConfigId)
                return string.Equals(candidate.Id.Trim(), requirement.Value, StringComparison.OrdinalIgnoreCase);

            if (string.Equals(candidate.Type.Trim(), source.Type.Trim(), StringComparison.OrdinalIgnoreCase) == false)
                return false;

            return string.Equals(candidate.Rarity.Trim(), requirement.Value, StringComparison.OrdinalIgnoreCase);
        }
    }
}

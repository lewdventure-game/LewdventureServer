using System.Globalization;
using Server.Common;
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

        private readonly RarityReader _rarityReader = new();

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

                var fields = part.Split(ValueSeparator, StringSplitOptions.RemoveEmptyEntries);

                if (fields.Length < 2)
                {
                    error = $"Equipment {source.Id} has an unreadable merge requirement {part}.";

                    return false;
                }

                var key = fields[0].Trim();
                var value = fields[1].Trim();
                var count = 1;

                if (2 < fields.Length)
                {
                    if (int.TryParse(fields[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count) == false || count <= 0)
                    {
                        error = $"Equipment {source.Id} has an unreadable count in merge requirement {part}.";

                        return false;
                    }
                }

                if (string.Equals(key, ConfigIdRequirement, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var configId) == false || configId <= 0)
                    {
                        error = $"Equipment {source.Id} has an unreadable equipment id in merge requirement {part}.";

                        return false;
                    }

                    requirements.Add(new EquipmentMergeRequirement(EquipmentMergeRequirementKind.ConfigId, configId, RarityType.Unknown, count));

                    continue;
                }

                if (string.Equals(key, RarityRequirement, StringComparison.OrdinalIgnoreCase))
                {
                    if (_rarityReader.TryRead(value, out var rarity) == false)
                    {
                        error = $"Equipment {source.Id} has an unknown rarity {value} in merge requirement {part}.";

                        return false;
                    }

                    requirements.Add(new EquipmentMergeRequirement(EquipmentMergeRequirementKind.Rarity, 0, rarity, count));

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

        public int CountRequiredItems(IReadOnlyList<EquipmentMergeRequirement> requirements)
        {
            var total = 0;

            for (int i = 0; i < requirements.Count; i++)
                total += requirements[i].Count;

            return total;
        }

        public bool TryMatchPayment(
            IEquipmentMapper source,
            IReadOnlyList<EquipmentMergeRequirement> requirements,
            IReadOnlyList<EquipmentMergeCandidate> candidates,
            out string error)
        {
            error = string.Empty;

            var required = CountRequiredItems(requirements);

            if (candidates.Count != required)
            {
                error = $"Transformation needs {required} items, got {candidates.Count}.";

                return false;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i].Kind != EquipmentMergeRequirementKind.ConfigId)
                    continue;

                if (TryUseCandidates(requirements[i], source, candidates) == false)
                {
                    error = $"Transformation needs {requirements[i].Count} equipment with id {requirements[i].ConfigId}.";

                    return false;
                }
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                if (requirements[i].Kind != EquipmentMergeRequirementKind.Rarity)
                    continue;

                if (TryUseCandidates(requirements[i], source, candidates) == false)
                {
                    error = $"Transformation needs {requirements[i].Count} equipment of type {source.Type} with rarity {requirements[i].Rarity}.";

                    return false;
                }
            }

            return true;
        }

        public bool TryResolveTarget(IEquipmentMapper source, IConfigDistributor configDistributor, out IEquipmentMapper target, out string error)
        {
            target = null!;
            error = string.Empty;

            if (source.MergeGroup <= 0)
            {
                error = $"Equipment {source.Id} has no merge_group.";

                return false;
            }

            var equipments = configDistributor.Equipments.Collection;

            for (int i = 0; i < equipments.Count; i++)
            {
                var candidate = equipments[i];

                if (candidate.MergeGroup != source.MergeGroup)
                    continue;

                if (candidate.MergeNumber != source.MergeNumber + 1)
                    continue;

                target = candidate;

                return true;
            }

            error = $"Merge group {source.MergeGroup} has no equipment with merge_number {source.MergeNumber + 1}.";

            return false;
        }

        private bool TryUseCandidates(
            in EquipmentMergeRequirement requirement,
            IEquipmentMapper source,
            IReadOnlyList<EquipmentMergeCandidate> candidates)
        {
            var taken = 0;

            for (int i = 0; i < candidates.Count && taken < requirement.Count; i++)
            {
                var candidate = candidates[i];

                if (candidate.IsUsed)
                    continue;

                if (Matches(requirement, source, candidate.Mapper) == false)
                    continue;

                candidate.IsUsed = true;
                taken += 1;
            }

            return taken == requirement.Count;
        }

        private bool Matches(in EquipmentMergeRequirement requirement, IEquipmentMapper source, IEquipmentMapper candidate)
        {
            if (requirement.Kind == EquipmentMergeRequirementKind.ConfigId)
                return candidate.Id == requirement.ConfigId;

            if (candidate.Type != source.Type)
                return false;

            return candidate.Rarity == requirement.Rarity;
        }
    }
}

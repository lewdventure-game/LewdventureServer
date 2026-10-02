using Server.Common;

namespace Server.Infrastructure.Players
{
    internal readonly struct EquipmentMergeRequirement
    {
        private readonly EquipmentMergeRequirementKind _kind;
        private readonly int _configId;
        private readonly RarityType _rarity;
        private readonly int _count;

        public EquipmentMergeRequirement(EquipmentMergeRequirementKind kind, int configId, RarityType rarity, int count)
        {
            _kind = kind;
            _configId = configId;
            _rarity = rarity;
            _count = count;
        }

        public EquipmentMergeRequirementKind Kind => _kind;

        public int ConfigId => _configId;

        public RarityType Rarity => _rarity;

        public int Count => _count;
    }
}

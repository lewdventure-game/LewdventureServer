using Server.Equipments;

namespace Server.Infrastructure.Players
{
    internal sealed class EquipmentSlotReader
    {
        private readonly Dictionary<string, EquipmentType> _bySlot = new(StringComparer.OrdinalIgnoreCase)
        {
            ["body"] = EquipmentType.Body,
            ["pants"] = EquipmentType.Pants,
            ["boots"] = EquipmentType.Boots,
            ["necklace"] = EquipmentType.Necklace,
        };

        public bool Matches(EquipmentType type, string slot)
        {
            if (_bySlot.TryGetValue(slot.Trim(), out var expected) == false)
                return false;

            return type == expected;
        }
    }
}

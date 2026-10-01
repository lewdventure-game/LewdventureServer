namespace Server.Infrastructure.Players
{
    internal readonly struct EquipmentMergeRequirement
    {
        private readonly EquipmentMergeRequirementKind _kind;
        private readonly string _value;

        public EquipmentMergeRequirement(EquipmentMergeRequirementKind kind, string value)
        {
            _kind = kind;
            _value = value;
        }

        public EquipmentMergeRequirementKind Kind => _kind;

        public string Value => _value;
    }
}

namespace Server.Bonuses
{
    internal sealed class BonusWorkMode
    {
        public BonusWorkMode(
            BonusWorkModeKind kind,
            string equippedEntityType,
            int equippedEntityId,
            int count)
        {
            Kind = kind;
            EquippedEntityType = equippedEntityType;
            EquippedEntityId = equippedEntityId;
            Count = count;
        }

        public BonusWorkModeKind Kind { get; }

        public string EquippedEntityType { get; }

        public int EquippedEntityId { get; }

        public int Count { get; }

        public static BonusWorkMode Unknown { get; } = new BonusWorkMode(
            BonusWorkModeKind.Unknown,
            string.Empty,
            0,
            0);
    }
}

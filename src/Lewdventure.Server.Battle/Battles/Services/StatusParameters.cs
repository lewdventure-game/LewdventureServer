namespace Server.Battles
{
    internal sealed class StatusParameters
    {
        public float DamageRatio { get; }

        public int DamageLength { get; }

        public int MaxStacks { get; }

        public IReadOnlyList<RewardBonus> Bonuses { get; }

        public StatusParameters(
            float damageRatio,
            int damageLength,
            int maxStacks,
            IReadOnlyList<RewardBonus> bonuses)
        {
            DamageRatio = damageRatio;
            DamageLength = damageLength;
            MaxStacks = maxStacks;
            Bonuses = bonuses;
        }
    }
}

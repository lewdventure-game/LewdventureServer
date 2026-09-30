namespace Server.Battles
{
    internal sealed class StatusParameters
    {
        public float DamageRatio { get; }

        public float FlatValue { get; }

        public int DamageLength { get; }

        public int MaxStacks { get; }

        public IReadOnlyList<RewardBonus> Bonuses { get; }

        public StatusParameters(
            float damageRatio,
            float flatValue,
            int damageLength,
            int maxStacks,
            IReadOnlyList<RewardBonus> bonuses)
        {
            DamageRatio = damageRatio;
            FlatValue = flatValue;
            DamageLength = damageLength;
            MaxStacks = maxStacks;
            Bonuses = bonuses;
        }
    }
}

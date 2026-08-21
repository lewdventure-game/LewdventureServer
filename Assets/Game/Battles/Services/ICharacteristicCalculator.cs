namespace Server.Battles
{
    internal interface ICharacteristicCalculator
    {
        public void ApplyToState(
            CharacteristicBuckets buckets,
            ICharacteristicState state,
            IReadOnlyList<ReplaceOverride> replaceOverrides,
            bool preserveCurrentHealthRatio,
            bool resetCurrentHealthToMax);

        public float CalculateVampyrismHeal(float dealtDamage, ICharacteristicState state);

        public float CalculateHealingFromMax(float maxHealth, float healingBonus, float healingBoost);

        public float CalculateHealingFromCurrent(float currentHealth, float healingBonus, float healingBoost);
    }
}

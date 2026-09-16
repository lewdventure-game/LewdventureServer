namespace Server.Battles
{
    internal sealed class CharacteristicBuckets
    {
        public float HealthBase;
        public float HealthLocal;
        public float HealthMinor;
        public float HealthMajor;
        public float HealthPerk;
        public float HealthGlobal;
        public float DamageBase;
        public float DamageLocal;
        public float DamageMinor;
        public float DamageMajor;
        public float DamagePerk;
        public float DamageGlobal;
        public float AttackMultiplierBase;
        public float AttackMultiplierLocal;
        public float AttackMultiplierPerk;
        public float AttackMultiplierGlobal;
        public float ArmorBase;
        public float ArmorLocal;
        public float ArmorPerk;
        public float ArmorGlobal;
        public float DefenceCoefficient;
        public float EvasionBase;
        public float EvasionLocal;
        public float EvasionPerk;
        public float EvasionGlobal;
        public float CriticalChanceBase;
        public float CriticalChanceLocal;
        public float CriticalChancePerk;
        public float CriticalChanceGlobal;
        public float CriticalMultiplierBase;
        public float CriticalMultiplierLocal;
        public float CriticalMultiplierPerk;
        public float CriticalMultiplierGlobal;
        public float Combo1ChanceBase;
        public float Combo1ChanceLocal;
        public float Combo1ChancePerk;
        public float Combo1ChanceGlobal;
        public float Combo2ChanceBase;
        public float Combo2ChanceLocal;
        public float Combo2ChancePerk;
        public float Combo2ChanceGlobal;
        public float ComboMultiplierBase;
        public float ComboMultiplierLocal;
        public float ComboMultiplierPerk;
        public float ComboMultiplierGlobal;
        public float CounterChanceBase;
        public float CounterChanceLocal;
        public float CounterChancePerk;
        public float CounterChanceGlobal;
        public float CounterMultiplierBase;
        public float CounterMultiplierLocal;
        public float CounterMultiplierPerk;
        public float CounterMultiplierGlobal;
        public float SkillMultiplierBase;
        public float SkillMultiplierLocal;
        public float SkillMultiplierPerk;
        public float SkillMultiplierGlobal;
        public float EnergyBase;
        public float EnergyLocal;
        public float EnergyPerk;
        public float EnergyGlobal;
        public float EnergyMaxBase;
        public float VampyrismBase;
        public float VampyrismLocal;
        public float VampyrismPerk;
        public float VampyrismGlobal;
        public float HealingBoostBase;
        public float HealingBoostLocal;
        public float HealingBoostPerk;
        public float HealingBoostGlobal;

        public CharacteristicBuckets Clone()
        {
            return (CharacteristicBuckets)MemberwiseClone();
        }
    }
}

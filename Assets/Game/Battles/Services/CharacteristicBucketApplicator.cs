using Server.Bonuses;

namespace Server.Battles
{
    internal static class CharacteristicBucketApplicator
    {
        public static void Apply(CharacteristicBuckets buckets, BonusType bonusType, float value)
        {
            switch (bonusType)
            {
                case BonusType.MaxHealthLocal:
                    buckets.HealthLocal += value;
                    break;
                case BonusType.MaxHealthMinor:
                    buckets.HealthMinor += value;
                    break;
                case BonusType.MaxHealthMajor:
                    buckets.HealthMajor += value;
                    break;
                case BonusType.MaxHealthPerk:
                    buckets.HealthPerk += value;
                    break;
                case BonusType.DamageLocal:
                    buckets.DamageLocal += value;
                    break;
                case BonusType.DamageMinor:
                    buckets.DamageMinor += value;
                    break;
                case BonusType.DamageMajor:
                    buckets.DamageMajor += value;
                    break;
                case BonusType.DamagePerk:
                    buckets.DamagePerk += value;
                    break;
                case BonusType.AttackMultiplierLocal:
                    buckets.AttackMultiplierLocal += value;
                    break;
                case BonusType.AttackMultiplierPerk:
                    buckets.AttackMultiplierPerk += value;
                    break;
                case BonusType.EvasionLocal:
                    buckets.EvasionLocal += value;
                    break;
                case BonusType.EvasionPerk:
                    buckets.EvasionPerk += value;
                    break;
                case BonusType.CriticalChanceLocal:
                    buckets.CriticalChanceLocal += value;
                    break;
                case BonusType.CriticalChancePerk:
                    buckets.CriticalChancePerk += value;
                    break;
                case BonusType.CriticalMultiplierLocal:
                    buckets.CriticalMultiplierLocal += value;
                    break;
                case BonusType.CriticalMultiplierPerk:
                    buckets.CriticalMultiplierPerk += value;
                    break;
                case BonusType.Combo1ChanceLocal:
                    buckets.Combo1ChanceLocal += value;
                    break;
                case BonusType.Combo1ChancePerk:
                    buckets.Combo1ChancePerk += value;
                    break;
                case BonusType.Combo2ChanceLocal:
                    buckets.Combo2ChanceLocal += value;
                    break;
                case BonusType.Combo2ChancePerk:
                    buckets.Combo2ChancePerk += value;
                    break;
                case BonusType.ComboMultiplierLocal:
                    buckets.Combo1MultiplierLocal += value;
                    buckets.Combo2MultiplierLocal += value;
                    break;
                case BonusType.ComboMultiplierPerk:
                    buckets.Combo1MultiplierPerk += value;
                    buckets.Combo2MultiplierPerk += value;
                    break;
                case BonusType.Combo1MultiplierLocal:
                    buckets.Combo1MultiplierLocal += value;
                    break;
                case BonusType.Combo1MultiplierPerk:
                    buckets.Combo1MultiplierPerk += value;
                    break;
                case BonusType.Combo2MultiplierLocal:
                    buckets.Combo2MultiplierLocal += value;
                    break;
                case BonusType.Combo2MultiplierPerk:
                    buckets.Combo2MultiplierPerk += value;
                    break;
                case BonusType.CounterChanceLocal:
                    buckets.CounterChanceLocal += value;
                    break;
                case BonusType.CounterChancePerk:
                    buckets.CounterChancePerk += value;
                    break;
                case BonusType.CounterMultiplierLocal:
                    buckets.CounterMultiplierLocal += value;
                    break;
                case BonusType.CounterMultiplierPerk:
                    buckets.CounterMultiplierPerk += value;
                    break;
                case BonusType.SpellMultiplierLocal:
                    buckets.SkillMultiplierLocal += value;
                    break;
                case BonusType.SpellMultiplierPerk:
                    buckets.SkillMultiplierPerk += value;
                    break;
                case BonusType.DefenceLocal:
                    buckets.DefenceLocal += value;
                    break;
                case BonusType.DefencePerk:
                    buckets.DefencePerk += value;
                    break;
                case BonusType.EnergyLocal:
                    buckets.EnergyLocal += value;
                    break;
                case BonusType.EnergyPerk:
                    buckets.EnergyPerk += value;
                    break;
                case BonusType.VampyrismLocal:
                    buckets.VampyrismLocal += value;
                    break;
                case BonusType.VampyrismPerk:
                    buckets.VampyrismPerk += value;
                    break;
                case BonusType.HealingBoostLocal:
                    buckets.HealingBoostLocal += value;
                    break;
                case BonusType.HealingBoostPerk:
                    buckets.HealingBoostPerk += value;
                    break;
            }
        }
    }
}

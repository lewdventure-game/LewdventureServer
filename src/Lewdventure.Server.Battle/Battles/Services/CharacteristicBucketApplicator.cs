using Microsoft.Extensions.Logging;
using Server.Bonuses;

namespace Server.Battles
{
    internal sealed class CharacteristicBucketApplicator : ICharacteristicBucketApplicator
    {
        private readonly ILogger<CharacteristicBucketApplicator> _logger;

        public CharacteristicBucketApplicator(ILogger<CharacteristicBucketApplicator> logger)
        {
            _logger = logger;
        }

        public void Apply(CharacteristicBuckets buckets, BonusType bonusType, float value)
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
                case BonusType.MaxHealthGlobal:
                    buckets.HealthGlobal += value;
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
                case BonusType.DamageGlobal:
                    buckets.DamageGlobal += value;
                    break;
                case BonusType.AttackMultiplierLocal:
                    buckets.AttackMultiplierLocal += value;
                    break;
                case BonusType.AttackMultiplierPerk:
                    buckets.AttackMultiplierPerk += value;
                    break;
                case BonusType.AttackMultiplierGlobal:
                    buckets.AttackMultiplierGlobal += value;
                    break;
                case BonusType.EvasionLocal:
                    buckets.EvasionLocal += value;
                    break;
                case BonusType.EvasionPerk:
                    buckets.EvasionPerk += value;
                    break;
                case BonusType.EvasionGlobal:
                    buckets.EvasionGlobal += value;
                    break;
                case BonusType.CriticalChanceLocal:
                    buckets.CriticalChanceLocal += value;
                    break;
                case BonusType.CriticalChancePerk:
                    buckets.CriticalChancePerk += value;
                    break;
                case BonusType.CriticalChanceGlobal:
                    buckets.CriticalChanceGlobal += value;
                    break;
                case BonusType.CriticalMultiplierLocal:
                    buckets.CriticalMultiplierLocal += value;
                    break;
                case BonusType.CriticalMultiplierPerk:
                    buckets.CriticalMultiplierPerk += value;
                    break;
                case BonusType.CriticalMultiplierGlobal:
                    buckets.CriticalMultiplierGlobal += value;
                    break;
                case BonusType.Combo1ChanceLocal:
                    buckets.Combo1ChanceLocal += value;
                    break;
                case BonusType.Combo1ChancePerk:
                    buckets.Combo1ChancePerk += value;
                    break;
                case BonusType.Combo1ChanceGlobal:
                    buckets.Combo1ChanceGlobal += value;
                    break;
                case BonusType.Combo2ChanceLocal:
                    buckets.Combo2ChanceLocal += value;
                    break;
                case BonusType.Combo2ChancePerk:
                    buckets.Combo2ChancePerk += value;
                    break;
                case BonusType.Combo2ChanceGlobal:
                    buckets.Combo2ChanceGlobal += value;
                    break;
                case BonusType.ComboMultiplierLocal:
                    buckets.ComboMultiplierLocal += value;
                    break;
                case BonusType.ComboMultiplierPerk:
                    buckets.ComboMultiplierPerk += value;
                    break;
                case BonusType.ComboMultiplierGlobal:
                    buckets.ComboMultiplierGlobal += value;
                    break;
                case BonusType.CounterChanceLocal:
                    buckets.CounterChanceLocal += value;
                    break;
                case BonusType.CounterChancePerk:
                    buckets.CounterChancePerk += value;
                    break;
                case BonusType.CounterChanceGlobal:
                    buckets.CounterChanceGlobal += value;
                    break;
                case BonusType.CounterMultiplierLocal:
                    buckets.CounterMultiplierLocal += value;
                    break;
                case BonusType.CounterMultiplierPerk:
                    buckets.CounterMultiplierPerk += value;
                    break;
                case BonusType.CounterMultiplierGlobal:
                    buckets.CounterMultiplierGlobal += value;
                    break;
                case BonusType.SpellMultiplierLocal:
                    buckets.SkillMultiplierLocal += value;
                    break;
                case BonusType.SpellMultiplierPerk:
                    buckets.SkillMultiplierPerk += value;
                    break;
                case BonusType.SpellMultiplierGlobal:
                    buckets.SkillMultiplierGlobal += value;
                    break;
                case BonusType.ArmorLocal:
                    buckets.ArmorLocal += value;
                    break;
                case BonusType.ArmorPerk:
                    buckets.ArmorPerk += value;
                    break;
                case BonusType.ArmorGlobal:
                    buckets.ArmorGlobal += value;
                    break;
                case BonusType.EnergyLocal:
                    buckets.EnergyLocal += value;
                    break;
                case BonusType.EnergyPerk:
                    buckets.EnergyPerk += value;
                    break;
                case BonusType.EnergyGlobal:
                    buckets.EnergyGlobal += value;
                    break;
                case BonusType.VampyrismLocal:
                    buckets.VampyrismLocal += value;
                    break;
                case BonusType.VampyrismPerk:
                    buckets.VampyrismPerk += value;
                    break;
                case BonusType.VampyrismGlobal:
                    buckets.VampyrismGlobal += value;
                    break;
                case BonusType.HealingBoostLocal:
                    buckets.HealingBoostLocal += value;
                    break;
                case BonusType.HealingBoostPerk:
                    buckets.HealingBoostPerk += value;
                    break;
                case BonusType.HealingBoostGlobal:
                    buckets.HealingBoostGlobal += value;
                    break;
                case BonusType.CurrentHealthLocal:
                case BonusType.Healing:
                case BonusType.HealingFromMax:
                    _logger.LogError($"[Story][Battle]: Bonus type not a characteristic bucket, bonusType = {bonusType}, value = {value}");

                    throw new InvalidOperationException($"[Story][Battle]: Bonus type not a characteristic bucket, bonusType = {bonusType}");
                default:
                    _logger.LogError($"[Story][Battle]: Unknown bonus type for bucket, bonusType = {bonusType}, value = {value}");

                    throw new InvalidOperationException($"[Story][Battle]: Unknown bonus type for bucket, bonusType = {bonusType}");
            }

            _logger.LogDebug($"[Story][Battle]: Bucket apply, bonusType = {bonusType}, value = {value}");
        }
    }
}

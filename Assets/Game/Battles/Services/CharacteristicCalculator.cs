using Microsoft.Extensions.Logging;
using Server.Bonuses;

namespace Server.Battles
{
    internal sealed class CharacteristicCalculator : ICharacteristicCalculator
    {
        private readonly ILogger<CharacteristicCalculator> _logger;

        public CharacteristicCalculator(ILogger<CharacteristicCalculator> logger)
        {
            _logger = logger;
        }

        public void ApplyToState(
            CharacteristicBuckets buckets,
            ICharacteristicState state,
            IReadOnlyList<ReplaceOverride> replaceOverrides,
            bool preserveCurrentHealthRatio,
            bool resetCurrentHealthToMax)
        {
            var previousMaxHealth = state.MaxHealth;
            var previousHealth = state.Health;

            var maxHealth = ApplyFormula1(buckets.HealthBase, buckets.HealthLocal, buckets.HealthMinor, buckets.HealthMajor, buckets.HealthPerk);
            var damage = ApplyFormula1(buckets.DamageBase, buckets.DamageLocal, buckets.DamageMinor, buckets.DamageMajor, buckets.DamagePerk);
            var defenceRaw = (buckets.DefenceBase + buckets.DefenceLocal) * (1f + buckets.DefencePerk);
            var defence = ApplyDefenceFormula(defenceRaw, buckets.DefenceCoefficient);
            var attackMultiplier = ApplyFormula2(buckets.AttackMultiplierBase, buckets.AttackMultiplierLocal, buckets.AttackMultiplierPerk);
            var evasion = ApplyFormula2(buckets.EvasionBase, buckets.EvasionLocal, buckets.EvasionPerk);
            var criticalChance = ApplyFormula2(buckets.CriticalChanceBase, buckets.CriticalChanceLocal, buckets.CriticalChancePerk);
            var criticalMultiplier = ApplyFormula2(buckets.CriticalMultiplierBase, buckets.CriticalMultiplierLocal, buckets.CriticalMultiplierPerk);
            var combo1Chance = ApplyFormula2(buckets.Combo1ChanceBase, buckets.Combo1ChanceLocal, buckets.Combo1ChancePerk);
            var combo2Chance = ApplyFormula2(buckets.Combo2ChanceBase, buckets.Combo2ChanceLocal, buckets.Combo2ChancePerk);
            var combo1Multiplier = ApplyFormula2(buckets.Combo1MultiplierBase, buckets.Combo1MultiplierLocal, buckets.Combo1MultiplierPerk);
            var combo2Multiplier = ApplyFormula2(buckets.Combo2MultiplierBase, buckets.Combo2MultiplierLocal, buckets.Combo2MultiplierPerk);
            var counterChance = ApplyFormula2(buckets.CounterChanceBase, buckets.CounterChanceLocal, buckets.CounterChancePerk);
            var counterMultiplier = ApplyFormula2(buckets.CounterMultiplierBase, buckets.CounterMultiplierLocal, buckets.CounterMultiplierPerk);
            var skillMultiplier = ApplyFormula2(buckets.SkillMultiplierBase, buckets.SkillMultiplierLocal, buckets.SkillMultiplierPerk);
            var energyGain = ApplyFormula2(buckets.EnergyBase, buckets.EnergyLocal, buckets.EnergyPerk);
            var vampyrism = ApplyFormula2(buckets.VampyrismBase, buckets.VampyrismLocal, buckets.VampyrismPerk);
            var healingBoost = ApplyFormula7(buckets.HealingBoostBase, buckets.HealingBoostLocal, buckets.HealingBoostPerk);

            ApplyReplaceOverrides(
                replaceOverrides,
                ref maxHealth,
                ref damage,
                ref defence,
                ref attackMultiplier,
                ref evasion,
                ref criticalChance,
                ref criticalMultiplier,
                ref combo1Chance,
                ref combo2Chance,
                ref combo1Multiplier,
                ref combo2Multiplier,
                ref counterChance,
                ref counterMultiplier,
                ref skillMultiplier,
                ref energyGain,
                ref vampyrism,
                ref healingBoost);

            criticalMultiplier = RoundFormula2Multiplier(criticalMultiplier, "criticalMultiplier");
            combo1Multiplier = RoundFormula2Multiplier(combo1Multiplier, "combo1Multiplier");
            combo2Multiplier = RoundFormula2Multiplier(combo2Multiplier, "combo2Multiplier");
            counterMultiplier = RoundFormula2Multiplier(counterMultiplier, "counterMultiplier");
            skillMultiplier = RoundFormula2Multiplier(skillMultiplier, "skillMultiplier");

            state.MaxHealth = maxHealth;
            state.Damage = damage;
            state.Defence = defence;
            state.AttackMultiplier = attackMultiplier;
            state.Evasion = evasion;
            state.CriticalChance = criticalChance;
            state.CriticalMultiplier = criticalMultiplier;
            state.Combo1Chance = combo1Chance;
            state.Combo2Chance = combo2Chance;
            state.Combo1Multiplier = combo1Multiplier;
            state.Combo2Multiplier = combo2Multiplier;
            state.CounterChance = counterChance;
            state.CounterMultiplier = counterMultiplier;
            state.SkillMultiplier = skillMultiplier;
            state.EnergyGain = energyGain;
            state.MaxEnergy = buckets.EnergyMaxBase;
            state.Vampyrism = vampyrism;
            state.HealingBoost = healingBoost;

            if (resetCurrentHealthToMax)
            {
                state.Health = maxHealth;
            }
            else if (preserveCurrentHealthRatio)
            {
                var ratio = 1f;

                if (0f < previousMaxHealth)
                    ratio = previousHealth / previousMaxHealth;

                state.Health = maxHealth * ratio;

                if (state.Health < 0f)
                    state.Health = 0f;
            }

            _logger.LogDebug($"[Story][Battle] characteristic rebuild maxHealth = {state.MaxHealth}, health = {state.Health}, damage = {state.Damage}, defence = {state.Defence}, combo1Mn = {state.Combo1Multiplier}, combo2Mn = {state.Combo2Multiplier}, vampyrism = {state.Vampyrism}, healingBoost = {state.HealingBoost}");
        }

        public float CalculateVampyrismHeal(float dealtDamage, ICharacteristicState state)
        {
            if (dealtDamage <= 0f || state.Vampyrism <= 0f)
                return 0f;

            var heal = MathF.Ceiling(dealtDamage * state.Vampyrism * state.HealingBoost);

            _logger.LogDebug($"[Story][Battle] vampyrism heal dealt = {dealtDamage}, vampyrism = {state.Vampyrism}, healingBoost = {state.HealingBoost}, heal = {heal}");

            return heal;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float CalculateHealingFromMax(float maxHealth, float healingBonus, float healingBoost)
        {
            return MathF.Ceiling(maxHealth * healingBonus * healingBoost);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public float CalculateHealingFromCurrent(float currentHealth, float healingBonus, float healingBoost)
        {
            return currentHealth * healingBonus * healingBoost;
        }

        private void ApplyReplaceOverrides(
            IReadOnlyList<ReplaceOverride> replaceOverrides,
            ref float maxHealth,
            ref float damage,
            ref float defence,
            ref float attackMultiplier,
            ref float evasion,
            ref float criticalChance,
            ref float criticalMultiplier,
            ref float combo1Chance,
            ref float combo2Chance,
            ref float combo1Multiplier,
            ref float combo2Multiplier,
            ref float counterChance,
            ref float counterMultiplier,
            ref float skillMultiplier,
            ref float energyGain,
            ref float vampyrism,
            ref float healingBoost)
        {
            for (int i = 0; i < replaceOverrides.Count; i++)
            {
                var replaceOverride = replaceOverrides[i];
                var value = replaceOverride.Value;

                switch (replaceOverride.BonusType)
                {
                    case BonusType.MaxHealthLocal:
                    case BonusType.MaxHealthMinor:
                    case BonusType.MaxHealthMajor:
                    case BonusType.MaxHealthPerk:
                        maxHealth = value;
                        break;
                    case BonusType.DamageLocal:
                    case BonusType.DamageMinor:
                    case BonusType.DamageMajor:
                    case BonusType.DamagePerk:
                        damage = value;
                        break;
                    case BonusType.DefenceLocal:
                    case BonusType.DefencePerk:
                        defence = value;
                        break;
                    case BonusType.AttackMultiplierLocal:
                    case BonusType.AttackMultiplierPerk:
                        attackMultiplier = value;
                        break;
                    case BonusType.EvasionLocal:
                    case BonusType.EvasionPerk:
                        evasion = value;
                        break;
                    case BonusType.CriticalChanceLocal:
                    case BonusType.CriticalChancePerk:
                        criticalChance = value;
                        break;
                    case BonusType.CriticalMultiplierLocal:
                    case BonusType.CriticalMultiplierPerk:
                        criticalMultiplier = value;
                        break;
                    case BonusType.Combo1ChanceLocal:
                    case BonusType.Combo1ChancePerk:
                        combo1Chance = value;
                        break;
                    case BonusType.Combo2ChanceLocal:
                    case BonusType.Combo2ChancePerk:
                        combo2Chance = value;
                        break;
                    case BonusType.ComboMultiplierLocal:
                    case BonusType.ComboMultiplierPerk:
                        combo1Multiplier = value;
                        combo2Multiplier = value;
                        break;
                    case BonusType.Combo1MultiplierLocal:
                    case BonusType.Combo1MultiplierPerk:
                        combo1Multiplier = value;
                        break;
                    case BonusType.Combo2MultiplierLocal:
                    case BonusType.Combo2MultiplierPerk:
                        combo2Multiplier = value;
                        break;
                    case BonusType.CounterChanceLocal:
                    case BonusType.CounterChancePerk:
                        counterChance = value;
                        break;
                    case BonusType.CounterMultiplierLocal:
                    case BonusType.CounterMultiplierPerk:
                        counterMultiplier = value;
                        break;
                    case BonusType.SpellMultiplierLocal:
                    case BonusType.SpellMultiplierPerk:
                        skillMultiplier = value;
                        break;
                    case BonusType.EnergyLocal:
                    case BonusType.EnergyPerk:
                        energyGain = value;
                        break;
                    case BonusType.VampyrismLocal:
                    case BonusType.VampyrismPerk:
                        vampyrism = value;
                        break;
                    case BonusType.HealingBoostLocal:
                    case BonusType.HealingBoostPerk:
                        healingBoost = value;
                        break;
                }
            }
        }

        private float ApplyFormula1(float baseValue, float local, float minor, float major, float perk)
        {
            var value = (baseValue + local) * (1f + minor) * (1f + major) * (1f + perk);
            value = MathF.Round(value);

            if (value < 1f)
                value = 1f;

            return value;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private float ApplyFormula2(float baseValue, float local, float perk)
        {
            return (baseValue + local) * (1f + perk);
        }

        private float RoundFormula2Multiplier(float value, string fieldName)
        {
            var rounded = MathF.Round(value);

            if (rounded != value)
                _logger.LogDebug($"[Story][Battle] formula2 multiplier round field = {fieldName} raw = {value} rounded = {rounded}");

            return rounded;
        }

        private float ApplyFormula7(float healingBoostBase, float local, float perk)
        {
            var value = MathF.Round((1f + healingBoostBase + local) * (1f + perk));

            if (value < 0f)
                value = 0f;

            return value;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private float ApplyDefenceFormula(float raw, float coefficient)
        {
            return coefficient <= 0f ? raw : coefficient * raw / (1f + coefficient * raw);
        }
    }
}

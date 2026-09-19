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

            var maxHealth = ApplyFormula1(
                buckets.HealthBase,
                buckets.HealthLocal,
                buckets.HealthMinor,
                buckets.HealthMajor,
                buckets.HealthPerk,
                buckets.HealthGlobal,
                "maxHealth");
            var damage = ApplyFormula1(
                buckets.DamageBase,
                buckets.DamageLocal,
                buckets.DamageMinor,
                buckets.DamageMajor,
                buckets.DamagePerk,
                buckets.DamageGlobal,
                "damage");
            var armor = ApplyFormula2(
                buckets.ArmorBase,
                buckets.ArmorLocal,
                buckets.ArmorPerk,
                buckets.ArmorGlobal,
                "armor");
            var attackMultiplier = ApplyFormula3(
                buckets.AttackMultiplierBase,
                buckets.AttackMultiplierLocal,
                buckets.AttackMultiplierPerk,
                buckets.AttackMultiplierGlobal,
                "attackMultiplier");
            var evasion = ApplyFormula3(
                buckets.EvasionBase,
                buckets.EvasionLocal,
                buckets.EvasionPerk,
                buckets.EvasionGlobal,
                "evasion");
            var criticalChance = ApplyFormula3(
                buckets.CriticalChanceBase,
                buckets.CriticalChanceLocal,
                buckets.CriticalChancePerk,
                buckets.CriticalChanceGlobal,
                "criticalChance");
            var criticalMultiplier = ApplyFormula3(
                buckets.CriticalMultiplierBase,
                buckets.CriticalMultiplierLocal,
                buckets.CriticalMultiplierPerk,
                buckets.CriticalMultiplierGlobal,
                "criticalMultiplier");
            var combo1Chance = ApplyFormula3(
                buckets.Combo1ChanceBase,
                buckets.Combo1ChanceLocal,
                buckets.Combo1ChancePerk,
                buckets.Combo1ChanceGlobal,
                "combo1Chance");
            var combo2Chance = ApplyFormula3(
                buckets.Combo2ChanceBase,
                buckets.Combo2ChanceLocal,
                buckets.Combo2ChancePerk,
                buckets.Combo2ChanceGlobal,
                "combo2Chance");
            var comboMultiplier = ApplyFormula3(
                buckets.ComboMultiplierBase,
                buckets.ComboMultiplierLocal,
                buckets.ComboMultiplierPerk,
                buckets.ComboMultiplierGlobal,
                "comboMultiplier");
            var counterChance = ApplyFormula3(
                buckets.CounterChanceBase,
                buckets.CounterChanceLocal,
                buckets.CounterChancePerk,
                buckets.CounterChanceGlobal,
                "counterChance");
            var counterMultiplier = ApplyFormula3(
                buckets.CounterMultiplierBase,
                buckets.CounterMultiplierLocal,
                buckets.CounterMultiplierPerk,
                buckets.CounterMultiplierGlobal,
                "counterMultiplier");
            var skillMultiplier = ApplyFormula3(
                buckets.SkillMultiplierBase,
                buckets.SkillMultiplierLocal,
                buckets.SkillMultiplierPerk,
                buckets.SkillMultiplierGlobal,
                "spellMultiplier");
            var energyGain = ApplyFormula2(
                buckets.EnergyBase,
                buckets.EnergyLocal,
                buckets.EnergyPerk,
                buckets.EnergyGlobal,
                "energyGain");
            var vampyrism = ApplyFormula2(
                buckets.VampyrismBase,
                buckets.VampyrismLocal,
                buckets.VampyrismPerk,
                buckets.VampyrismGlobal,
                "vampyrism");
            var healingBoost = ApplyFormula7(
                buckets.HealingBoostBase,
                buckets.HealingBoostLocal,
                buckets.HealingBoostPerk,
                buckets.HealingBoostGlobal);

            if (vampyrism < 0f)
                vampyrism = 0f;

            ApplyReplaceOverrides(
                replaceOverrides,
                ref maxHealth,
                ref damage,
                ref armor,
                ref attackMultiplier,
                ref evasion,
                ref criticalChance,
                ref criticalMultiplier,
                ref combo1Chance,
                ref combo2Chance,
                ref comboMultiplier,
                ref counterChance,
                ref counterMultiplier,
                ref skillMultiplier,
                ref energyGain,
                ref vampyrism,
                ref healingBoost);

            var defence = ApplyDefenceFormula(armor, buckets.DefenceCoefficient);

            criticalMultiplier = RoundMultiplier(criticalMultiplier, "criticalMultiplier");
            comboMultiplier = RoundMultiplier(comboMultiplier, "comboMultiplier");
            counterMultiplier = RoundMultiplier(counterMultiplier, "counterMultiplier");
            skillMultiplier = RoundMultiplier(skillMultiplier, "spellMultiplier");

            state.MaxHealth = maxHealth;
            state.Damage = damage;
            state.Armor = armor;
            state.Defence = defence;
            state.AttackMultiplier = attackMultiplier;
            state.Evasion = evasion;
            state.CriticalChance = criticalChance;
            state.CriticalMultiplier = criticalMultiplier;
            state.Combo1Chance = combo1Chance;
            state.Combo2Chance = combo2Chance;
            state.ComboMultiplier = comboMultiplier;
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

            _logger.LogInformation($"[Story][Battle]: Characteristic rebuild, maxHealth = {state.MaxHealth}, health = {state.Health}, damage = {state.Damage}, armor = {state.Armor}, defence = {state.Defence}, comboMn = {state.ComboMultiplier}, vampyrism = {state.Vampyrism}, healingBoost = {state.HealingBoost}");
        }

        public float CalculateVampyrismHeal(float dealtDamage, ICharacteristicState state)
        {
            if (dealtDamage <= 0f || state.Vampyrism <= 0f)
                return 0f;

            var heal = MathF.Ceiling(dealtDamage * state.Vampyrism * state.HealingBoost);

            if (heal < 0f)
                heal = 0f;

            _logger.LogDebug($"[Story][Battle]: Vampyrism heal, dealt = {dealtDamage}, vampyrism = {state.Vampyrism}, healingBoost = {state.HealingBoost}, heal = {heal}");

            return heal;
        }

        public float CalculateHealingFromMax(float maxHealth, float healingBonus, float healingBoost)
        {
            var healDelta = maxHealth * healingBonus * healingBoost;

            _logger.LogDebug($"[Story][Battle]: Healing from max, maxHealth = {maxHealth}, healingBonus = {healingBonus}, healingBoost = {healingBoost}, healDelta = {healDelta}");

            return healDelta;
        }

        public float CalculateHealingFromCurrent(float currentHealth, float healingBonus, float healingBoost)
        {
            var healDelta = currentHealth * healingBonus * healingBoost;

            _logger.LogDebug($"[Story][Battle]: Healing from current, currentHealth = {currentHealth}, healingBonus = {healingBonus}, healingBoost = {healingBoost}, healDelta = {healDelta}");

            return healDelta;
        }

        private void ApplyReplaceOverrides(
            IReadOnlyList<ReplaceOverride> replaceOverrides,
            ref float maxHealth,
            ref float damage,
            ref float armor,
            ref float attackMultiplier,
            ref float evasion,
            ref float criticalChance,
            ref float criticalMultiplier,
            ref float combo1Chance,
            ref float combo2Chance,
            ref float comboMultiplier,
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
                    case BonusType.MaxHealthGlobal:
                        maxHealth = value;
                        break;
                    case BonusType.DamageLocal:
                    case BonusType.DamageMinor:
                    case BonusType.DamageMajor:
                    case BonusType.DamagePerk:
                    case BonusType.DamageGlobal:
                        damage = value;
                        break;
                    case BonusType.ArmorLocal:
                    case BonusType.ArmorPerk:
                    case BonusType.ArmorGlobal:
                        armor = value;
                        break;
                    case BonusType.AttackMultiplierLocal:
                    case BonusType.AttackMultiplierPerk:
                    case BonusType.AttackMultiplierGlobal:
                        attackMultiplier = value;
                        break;
                    case BonusType.EvasionLocal:
                    case BonusType.EvasionPerk:
                    case BonusType.EvasionGlobal:
                        evasion = value;
                        break;
                    case BonusType.CriticalChanceLocal:
                    case BonusType.CriticalChancePerk:
                    case BonusType.CriticalChanceGlobal:
                        criticalChance = value;
                        break;
                    case BonusType.CriticalMultiplierLocal:
                    case BonusType.CriticalMultiplierPerk:
                    case BonusType.CriticalMultiplierGlobal:
                        criticalMultiplier = value;
                        break;
                    case BonusType.Combo1ChanceLocal:
                    case BonusType.Combo1ChancePerk:
                    case BonusType.Combo1ChanceGlobal:
                        combo1Chance = value;
                        break;
                    case BonusType.Combo2ChanceLocal:
                    case BonusType.Combo2ChancePerk:
                    case BonusType.Combo2ChanceGlobal:
                        combo2Chance = value;
                        break;
                    case BonusType.ComboMultiplierLocal:
                    case BonusType.ComboMultiplierPerk:
                    case BonusType.ComboMultiplierGlobal:
                        comboMultiplier = value;
                        break;
                    case BonusType.CounterChanceLocal:
                    case BonusType.CounterChancePerk:
                    case BonusType.CounterChanceGlobal:
                        counterChance = value;
                        break;
                    case BonusType.CounterMultiplierLocal:
                    case BonusType.CounterMultiplierPerk:
                    case BonusType.CounterMultiplierGlobal:
                        counterMultiplier = value;
                        break;
                    case BonusType.SpellMultiplierLocal:
                    case BonusType.SpellMultiplierPerk:
                    case BonusType.SpellMultiplierGlobal:
                        skillMultiplier = value;
                        break;
                    case BonusType.EnergyLocal:
                    case BonusType.EnergyPerk:
                    case BonusType.EnergyGlobal:
                        energyGain = value;
                        break;
                    case BonusType.VampyrismLocal:
                    case BonusType.VampyrismPerk:
                    case BonusType.VampyrismGlobal:
                        vampyrism = value;
                        break;
                    case BonusType.HealingBoostLocal:
                    case BonusType.HealingBoostPerk:
                    case BonusType.HealingBoostGlobal:
                        healingBoost = value;
                        break;
                    default:
                        _logger.LogError($"[Story][Battle]: Unknown replace bonus type, bonusType = {replaceOverride.BonusType}");

                        throw new InvalidOperationException($"[Story][Battle]: Unknown replace bonus type, bonusType = {replaceOverride.BonusType}");
                }
            }
        }

        private float ApplyFormula1(
            float baseValue,
            float local,
            float minor,
            float major,
            float perk,
            float global,
            string fieldName)
        {
            var value = (baseValue + local) * (1f + minor) * (1f + major) * (1f + perk) * (1f + global);
            var rounded = RoundMathematical(value);

            if (rounded < 1f)
                rounded = 1f;

            _logger.LogDebug($"[Story][Battle]: Formula1, field = {fieldName}, base = {baseValue}, local = {local}, minor = {minor}, major = {major}, perk = {perk}, global = {global}, raw = {value}, result = {rounded}");

            return rounded;
        }

        private float ApplyFormula2(float baseValue, float local, float perk, float global, string fieldName)
        {
            var value = (baseValue + local) * (1f + perk) * (1f + global);

            _logger.LogDebug($"[Story][Battle]: Formula2, field = {fieldName}, base = {baseValue}, local = {local}, perk = {perk}, global = {global}, result = {value}");

            return value;
        }

        private float ApplyFormula3(float baseValue, float local, float perk, float global, string fieldName)
        {
            var value = (1f + baseValue + local) * (1f + perk) * (1f + global);

            if (value < 0f)
                value = 0f;

            _logger.LogDebug($"[Story][Battle]: Formula3, field = {fieldName}, base = {baseValue}, local = {local}, perk = {perk}, global = {global}, result = {value}");

            return value;
        }

        private float ApplyFormula7(float healingBoostBase, float local, float perk, float global)
        {
            var value = (1f + healingBoostBase + local) * (1f + perk) * (1f + global);
            var rounded = RoundMathematical(value);

            if (rounded < 0f)
                rounded = 0f;

            _logger.LogDebug($"[Story][Battle]: Formula7, base = {healingBoostBase}, local = {local}, perk = {perk}, global = {global}, raw = {value}, result = {rounded}");

            return rounded;
        }

        private float RoundMultiplier(float value, string fieldName)
        {
            var rounded = RoundMathematical(value);

            if (rounded != value)
                _logger.LogDebug($"[Story][Battle]: Multiplier round, field = {fieldName}, raw = {value}, rounded = {rounded}");

            return rounded;
        }

        private float RoundMathematical(float value)
        {
            return MathF.Round(value, MidpointRounding.AwayFromZero);
        }

        private float ApplyDefenceFormula(float armor, float coefficient)
        {
            if (coefficient <= 0f)
            {
                _logger.LogError($"[Story][Battle]: Defence formula missing coefficient, coefficient = {coefficient}, armor = {armor}");

                throw new InvalidOperationException($"[Story][Battle]: defence_coeff missing or <= 0, coefficient = {coefficient}");
            }

            var absoluteArmor = armor;

            if (absoluteArmor < 0f)
                absoluteArmor = -absoluteArmor;

            var defence = coefficient * armor / (1f + coefficient * absoluteArmor);

            _logger.LogDebug($"[Story][Battle]: Formula5, armor = {armor}, absoluteArmor = {absoluteArmor}, coefficient = {coefficient}, defence = {defence}");

            return defence;
        }
    }
}

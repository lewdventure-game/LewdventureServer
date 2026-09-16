using System.Runtime.Serialization;

namespace Server.Bonuses
{
    internal enum BonusType
    {
        Unknown = 0,
        MaxHealthLocal = 1,
        MaxHealthMinor = 2,
        MaxHealthMajor = 3,
        MaxHealthPerk = 4,
        CurrentHealthLocal = 5,
        VampyrismLocal = 6,
        VampyrismPerk = 7,
        HealingBoostLocal = 8,
        HealingBoostPerk = 9,
        DamageLocal = 10,
        DamageMinor = 11,
        DamageMajor = 12,
        DamagePerk = 13,
        AttackMultiplierLocal = 14,
        AttackMultiplierPerk = 15,
        EvasionLocal = 16,
        EvasionPerk = 17,
        [EnumMember(Value = "crit_chance_local")]
        CriticalChanceLocal = 18,
        [EnumMember(Value = "crit_chance_perk")]
        CriticalChancePerk = 19,
        [EnumMember(Value = "crit_multiplier_local")]
        CriticalMultiplierLocal = 20,
        [EnumMember(Value = "crit_multiplier_perk")]
        CriticalMultiplierPerk = 21,
        [EnumMember(Value = "combo_1_chance_local")]
        Combo1ChanceLocal = 22,
        [EnumMember(Value = "combo_1_chance_perk")]
        Combo1ChancePerk = 23,
        [EnumMember(Value = "combo_2_chance_local")]
        Combo2ChanceLocal = 24,
        [EnumMember(Value = "combo_2_chance_perk")]
        Combo2ChancePerk = 25,
        [EnumMember(Value = "combo_multiplier_local")]
        ComboMultiplierLocal = 26,
        [EnumMember(Value = "combo_multiplier_perk")]
        ComboMultiplierPerk = 27,
        CounterChanceLocal = 28,
        CounterChancePerk = 29,
        CounterMultiplierLocal = 30,
        CounterMultiplierPerk = 31,
        SpellMultiplierLocal = 32,
        SpellMultiplierPerk = 33,
        DefenceLocal = 34,
        DefencePerk = 35,
        EnergyLocal = 36,
        EnergyPerk = 37,
        HealingFromMax = 38,
        Healing = 39,
        [EnumMember(Value = "combo_1_multiplier_local")]
        Combo1MultiplierLocal = 40,
        [EnumMember(Value = "combo_1_multiplier_perk")]
        Combo1MultiplierPerk = 41,
        [EnumMember(Value = "combo_2_multiplier_local")]
        Combo2MultiplierLocal = 42,
        [EnumMember(Value = "combo_2_multiplier_perk")]
        Combo2MultiplierPerk = 43,
    }
}

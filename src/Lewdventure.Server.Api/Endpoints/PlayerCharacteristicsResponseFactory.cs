using Server.Battles;

namespace Server.Api.Endpoints
{
    internal sealed class PlayerCharacteristicsResponseFactory
    {
        public PlayerCharacteristicsResponse Create(ICharacteristicState characteristics)
        {
            return new PlayerCharacteristicsResponse
            {
                Health = characteristics.Health,
                MaxHealth = characteristics.MaxHealth,
                Damage = characteristics.Damage,
                AttackMultiplier = characteristics.AttackMultiplier,
                Armor = characteristics.Armor,
                Defence = characteristics.Defence,
                Evasion = characteristics.Evasion,
                CriticalChance = characteristics.CriticalChance,
                CriticalMultiplier = characteristics.CriticalMultiplier,
                Combo1Chance = characteristics.Combo1Chance,
                Combo2Chance = characteristics.Combo2Chance,
                ComboMultiplier = characteristics.ComboMultiplier,
                Combo2Multiplier = characteristics.Combo2Multiplier,
                CounterChance = characteristics.CounterChance,
                CounterMultiplier = characteristics.CounterMultiplier,
                Energy = characteristics.Energy,
                EnergyGain = characteristics.EnergyGain,
                MaxEnergy = characteristics.MaxEnergy,
                SkillMultiplier = characteristics.SkillMultiplier,
                Vampyrism = characteristics.Vampyrism,
                HealingBoost = characteristics.HealingBoost,
            };
        }
    }
}

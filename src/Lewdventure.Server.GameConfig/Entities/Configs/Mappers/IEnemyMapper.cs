using Server.Configs;

namespace Server.Entities
{
    internal interface IEnemyMapper : IConfigMapper
    {
        public int Id { get; }

        public string SkinName { get; }

        public bool IsMelee { get; }

        public float Health { get; }

        public float Damage { get; }

        public int[] SkillIds { get; }

        public float Defence { get; }

        public float Evasion { get; }

        public float Vampyrism { get; }

        public float HealingBoost { get; }

        public float CriticalChance { get; }

        public float CriticalMultiplier { get; }

        public float Combo1Chance { get; }

        public float Combo2Chance { get; }

        public float Combo1Multiplier { get; }

        public float Combo2Multiplier { get; }

        public float CounterChance { get; }

        public float CounterMultiplier { get; }

        public float SpellMultiplier { get; }

        public float Energy { get; }

        public float MaxEnergy { get; }
    }
}

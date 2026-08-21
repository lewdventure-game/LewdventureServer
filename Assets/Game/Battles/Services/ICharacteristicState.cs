namespace Server.Battles
{
    internal interface ICharacteristicState
    {
        public float Health { get; set; }

        public float MaxHealth { get; set; }

        public float Damage { get; set; }

        public float AttackMultiplier { get; set; }

        public float Defence { get; set; }

        public float Evasion { get; set; }

        public float CriticalChance { get; set; }

        public float CriticalMultiplier { get; set; }

        public float CounterChance { get; set; }

        public float CounterMultiplier { get; set; }

        public float Combo1Chance { get; set; }

        public float Combo1Multiplier { get; set; }

        public float Combo2Chance { get; set; }

        public float Combo2Multiplier { get; set; }

        public float Energy { get; set; }

        public float EnergyGain { get; set; }

        public float MaxEnergy { get; set; }

        public float SkillMultiplier { get; set; }

        public float Vampyrism { get; set; }

        public float HealingBoost { get; set; }
    }
}

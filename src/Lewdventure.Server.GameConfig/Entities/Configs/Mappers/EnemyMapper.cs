using System.Runtime.Serialization;
using Newtonsoft.Json;
using Server.Configs;

namespace Server.Entities
{
    internal sealed class EnemyMapper : IEnemyMapper
    {
        private float _defence;
        private float _evasion;
        private float _vampyrism;
        private float _healingBoost;
        private float _criticalChance;
        private float _criticalMultiplier;
        private float _comboChance;
        private float _combo2Chance;
        private float _comboMultiplier;
        private float _counterChance;
        private float _counterMultiplier;
        private float _spellMultiplier = 1f;
        private float _energy;
        private float _maxEnergy;

        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("skin_name")]
        public string SkinName { get; init; } = string.Empty;

        [JsonProperty("is_melee")]
        public bool IsMelee { get; init; }

        [JsonProperty("health")]
        public float Health { get; init; }

        [JsonProperty("damage")]
        public float Damage { get; init; }

        [JsonProperty("skill_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] SkillIds { get; init; } = Array.Empty<int>();

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("evasion")]
        private float EvasionRaw
        {
            set { _evasion = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("crit_chance")]
        private float CriticalChanceRaw
        {
            set { _criticalChance = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("crit_multiplier")]
        private float CriticalMultiplierRaw
        {
            set { _criticalMultiplier = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("combo_chance")]
        private float ComboChanceRaw
        {
            set { _comboChance = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("combo_multiplier")]
        private float ComboMultiplierRaw
        {
            set { _comboMultiplier = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("counter_chance")]
        private float CounterChanceRaw
        {
            set { _counterChance = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("counter_multiplier")]
        private float CounterMultiplierRaw
        {
            set { _counterMultiplier = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("defence")]
        private float DefenceRaw
        {
            set { _defence = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("energy_per_hit")]
        private float EnergyPerHitRaw
        {
            set { _energy = value; }
        }

        [OptionalColumn("значение приходит в other_characteristics")]
        [JsonProperty("energy_max")]
        private float EnergyMaxRaw
        {
            set { _maxEnergy = value; }
        }

        [JsonProperty("other_characteristics")]
        private string _characteristicsRaw = string.Empty;

        public float Defence => _defence;

        public float Evasion => _evasion;

        public float Vampyrism => _vampyrism;

        public float HealingBoost => _healingBoost;

        public float CriticalChance => _criticalChance;

        public float CriticalMultiplier => _criticalMultiplier;

        public float Combo1Chance => _comboChance;

        public float Combo2Chance => _combo2Chance;

        public float ComboMultiplier => _comboMultiplier;

        public float CounterChance => _counterChance;

        public float CounterMultiplier => _counterMultiplier;

        public float SpellMultiplier => _spellMultiplier;

        public float Energy => _energy;

        public float MaxEnergy => _maxEnergy;

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (string.IsNullOrWhiteSpace(_characteristicsRaw))
                return;

            var packed = new EnemyCharacteristics(_characteristicsRaw);

            _defence = packed.Defence;
            _evasion = packed.Evasion;
            _vampyrism = packed.Vampyrism;
            _healingBoost = packed.HealingBoost;
            _criticalChance = packed.CriticalChance;
            _criticalMultiplier = packed.CriticalMultiplier;
            _comboChance = packed.Combo1Chance;
            _combo2Chance = packed.Combo2Chance;
            _comboMultiplier = packed.ComboMultiplier;
            _counterChance = packed.CounterChance;
            _counterMultiplier = packed.CounterMultiplier;
            _spellMultiplier = packed.SpellMultiplier;
            _energy = packed.Energy;
            _maxEnergy = packed.EnergyMax;
        }
    }
}

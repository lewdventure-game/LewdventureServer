using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Server.Entities
{
    // Sheets may ship either flat client columns or packed other_characteristics.
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
        private float _combo1Multiplier;
        private float _combo2Multiplier;
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

        [JsonProperty("evasion")]
        private float EvasionRaw
        {
            set { _evasion = value; }
        }

        [JsonProperty("crit_chance")]
        private float CriticalChanceRaw
        {
            set { _criticalChance = value; }
        }

        [JsonProperty("crit_multiplier")]
        private float CriticalMultiplierRaw
        {
            set { _criticalMultiplier = value; }
        }

        [JsonProperty("combo_chance")]
        private float ComboChanceRaw
        {
            set { _comboChance = value; }
        }

        [JsonProperty("combo_1_multiplier")]
        private float Combo1MultiplierRaw
        {
            set { _combo1Multiplier = value; }
        }

        [JsonProperty("combo_2_multiplier")]
        private float Combo2MultiplierRaw
        {
            set { _combo2Multiplier = value; }
        }

        [JsonProperty("combo_multiplier")]
        private float ComboMultiplierRaw
        {
            set
            {
                _combo1Multiplier = value;
                _combo2Multiplier = value;
            }
        }

        [JsonProperty("counter_chance")]
        private float CounterChanceRaw
        {
            set { _counterChance = value; }
        }

        [JsonProperty("counter_multiplier")]
        private float CounterMultiplierRaw
        {
            set { _counterMultiplier = value; }
        }

        [JsonProperty("defence")]
        private float DefenceRaw
        {
            set { _defence = value; }
        }

        [JsonProperty("energy_per_hit")]
        private float EnergyPerHitRaw
        {
            set { _energy = value; }
        }

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

        public float Combo1Multiplier => _combo1Multiplier;

        public float Combo2Multiplier => _combo2Multiplier;

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

            var packed = EnemyCharacteristics.Parse(_characteristicsRaw);

            _defence = packed.Defence;
            _evasion = packed.Evasion;
            _vampyrism = packed.Vampyrism;
            _healingBoost = packed.HealingBoost;
            _criticalChance = packed.CriticalChance;
            _criticalMultiplier = packed.CriticalMultiplier;
            _comboChance = packed.Combo1Chance;
            _combo2Chance = packed.Combo2Chance;
            _combo1Multiplier = packed.Combo1Multiplier;
            _combo2Multiplier = packed.Combo2Multiplier;
            _counterChance = packed.CounterChance;
            _counterMultiplier = packed.CounterMultiplier;
            _spellMultiplier = packed.SpellMultiplier;
            _energy = packed.Energy;
            _maxEnergy = packed.EnergyMax;
        }
    }
}

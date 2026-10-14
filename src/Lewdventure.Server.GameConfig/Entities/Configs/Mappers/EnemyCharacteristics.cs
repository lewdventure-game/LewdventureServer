using System.Globalization;

namespace Server.Entities
{
    internal sealed class EnemyCharacteristics
    {
        private float _armor;
        private float _evasion;
        private float _vampyrism;
        private float _healingBoost;
        private float _criticalChance;
        private float _criticalMultiplier;
        private float _combo1Chance;
        private float _combo2Chance;
        private float _comboMultiplier;
        private float _counterChance;
        private float _counterMultiplier;
        private float _spellMultiplier = 1f;
        private float _energy;
        private float _energyMax;

        public float Armor => _armor;

        public float Evasion => _evasion;

        public float Vampyrism => _vampyrism;

        public float HealingBoost => _healingBoost;

        public float CriticalChance => _criticalChance;

        public float CriticalMultiplier => _criticalMultiplier;

        public float Combo1Chance => _combo1Chance;

        public float Combo2Chance => _combo2Chance;

        public float ComboMultiplier => _comboMultiplier;

        public float CounterChance => _counterChance;

        public float CounterMultiplier => _counterMultiplier;

        public float SpellMultiplier => _spellMultiplier;

        public float Energy => _energy;

        public float EnergyMax => _energyMax;

        public EnemyCharacteristics(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            var span = raw.AsSpan();
            var start = 0;

            for (int i = 0; i <= span.Length; i++)
            {
                if (i < span.Length && span[i] != ';')
                    continue;

                var segmentLength = i - start;

                if (0 < segmentLength)
                    ApplySegment(span.Slice(start, segmentLength));

                start = i + 1;
            }
        }

        private void ApplySegment(ReadOnlySpan<char> segment)
        {
            segment = segment.Trim();

            if (segment.Length == 0)
                return;

            var colon = segment.IndexOf(':');

            if (colon <= 0)
                return;

            var key = segment.Slice(0, colon).Trim();
            var value = ParseBracketValue(segment.Slice(colon + 1).Trim());

            if (key.Equals("armor", StringComparison.OrdinalIgnoreCase)
                || key.Equals("defence", StringComparison.OrdinalIgnoreCase))
                _armor = value;
            else if (key.Equals("evasion", StringComparison.OrdinalIgnoreCase))
                _evasion = value;
            else if (key.Equals("vampyrism", StringComparison.OrdinalIgnoreCase))
                _vampyrism = value;
            else if (key.Equals("healing_boost", StringComparison.OrdinalIgnoreCase))
                _healingBoost = value;
            else if (key.Equals("crit_chance", StringComparison.OrdinalIgnoreCase))
                _criticalChance = value;
            else if (key.Equals("crit_multiplier", StringComparison.OrdinalIgnoreCase))
                _criticalMultiplier = value;
            else if (key.Equals("combo_chance", StringComparison.OrdinalIgnoreCase)
                || key.Equals("combo_1_chance", StringComparison.OrdinalIgnoreCase))
                _combo1Chance = value;
            else if (key.Equals("combo_2_chance", StringComparison.OrdinalIgnoreCase))
                _combo2Chance = value;
            else if (key.Equals("combo_multiplier", StringComparison.OrdinalIgnoreCase))
                _comboMultiplier = value;
            else if (key.Equals("counter_chance", StringComparison.OrdinalIgnoreCase))
                _counterChance = value;
            else if (key.Equals("counter_multiplier", StringComparison.OrdinalIgnoreCase))
                _counterMultiplier = value;
            else if (key.Equals("spell_multiplier", StringComparison.OrdinalIgnoreCase))
                _spellMultiplier = value;
            else if (key.Equals("energy", StringComparison.OrdinalIgnoreCase)
                || key.Equals("energy_per_hit", StringComparison.OrdinalIgnoreCase))
                _energy = value;
            else if (key.Equals("energy_max", StringComparison.OrdinalIgnoreCase))
                _energyMax = value;
        }

        private float ParseBracketValue(ReadOnlySpan<char> valueSpan)
        {
            valueSpan = valueSpan.Trim();

            if (valueSpan.Length == 0)
                return 0f;

            var open = valueSpan.IndexOf('[');
            var close = valueSpan.IndexOf(']');

            if (0 <= open && open < close)
                valueSpan = valueSpan.Slice(open + 1, close - open - 1).Trim();

            if (valueSpan.Length == 0)
                return 0f;

            if (float.TryParse(
                valueSpan,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value))
                return value;

            return 0f;
        }
    }
}

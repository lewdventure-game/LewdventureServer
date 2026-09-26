using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Server.Configs;
using Server.Statuses;

namespace Server.Battles
{
    internal sealed class StatusParametersParser : IStatusParametersParser
    {
        private const string BonusesKey = "bonuses";
        private const string DamageLengthKey = "damage_length";
        private const string DamageRatioKey = "damage_ratio";
        private const string MaxStacksKey = "max_stacks";

        private readonly ILogger<StatusParametersParser> _logger;

        public StatusParametersParser(ILogger<StatusParametersParser> logger)
        {
            _logger = logger;
        }

        public bool TryParse(string parameters, StatusType statusType, [MaybeNullWhen(false)] out StatusParameters parsed)
        {
            parsed = null;

            if (string.IsNullOrWhiteSpace(parameters))
            {
                _logger.LogError($"[Config]: Status parameters empty, statusType = {statusType}");

                return false;
            }

            var pairs = SplitTopLevel(parameters, SeparatorFormat.PairSeparator);
            var hasDamageRatio = false;
            var hasDamageLength = false;
            var hasMaxStacks = false;
            var hasBonuses = false;
            var damageRatio = 0f;
            var damageLength = 0;
            var maxStacks = 0;
            IReadOnlyList<RewardBonus> bonuses = Array.Empty<RewardBonus>();

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i].AsSpan().Trim();

                if (pair.IsEmpty)
                    continue;

                var separator = pair.IndexOf(SeparatorFormat.KeyValueSeparator);

                if (separator < 0)
                {
                    _logger.LogError($"[Config]: Status parameters pair without key, statusType = {statusType}, raw = {parameters}");

                    return false;
                }

                var key = pair.Slice(0, separator).Trim().ToString();
                var value = UnwrapBrackets(pair.Slice(separator + 1).Trim());

                if (key == DamageRatioKey)
                {
                    if (TryParseFloat(value, out damageRatio) == false)
                    {
                        _logger.LogError($"[Config]: Status damage_ratio invalid, statusType = {statusType}, raw = {parameters}");

                        return false;
                    }

                    hasDamageRatio = true;
                    continue;
                }

                if (key == DamageLengthKey)
                {
                    if (TryParseInt(value, out damageLength) == false)
                    {
                        _logger.LogError($"[Config]: Status damage_length invalid, statusType = {statusType}, raw = {parameters}");

                        return false;
                    }

                    hasDamageLength = true;
                    continue;
                }

                if (key == MaxStacksKey)
                {
                    if (TryParseInt(value, out maxStacks) == false)
                    {
                        _logger.LogError($"[Config]: Status max_stacks invalid, statusType = {statusType}, raw = {parameters}");

                        return false;
                    }

                    hasMaxStacks = true;
                    continue;
                }

                if (key == BonusesKey)
                {
                    var bonusSeparator = ResolveBonusListSeparator(statusType);

                    if (bonusSeparator == 0)
                    {
                        _logger.LogError($"[Config]: Status bonuses unexpected, statusType = {statusType}, raw = {parameters}");

                        return false;
                    }

                    if (TryParseBonusList(value, bonusSeparator, statusType, parameters, out bonuses) == false)
                        return false;

                    hasBonuses = true;
                    continue;
                }

                _logger.LogError($"[Config]: Status parameters unknown key = {key}, statusType = {statusType}, raw = {parameters}");

                return false;
            }

            if (IsDamageOverTime(statusType))
            {
                if (hasDamageRatio == false || hasDamageLength == false || hasMaxStacks == false)
                {
                    _logger.LogError($"[Config]: Status damage over time keys missing, statusType = {statusType}, raw = {parameters}");

                    return false;
                }

                if (damageLength <= 0 || maxStacks <= 0)
                {
                    _logger.LogError($"[Config]: Status damage over time values invalid, statusType = {statusType}, damageLength = {damageLength}, maxStacks = {maxStacks}, raw = {parameters}");

                    return false;
                }
            }

            if (IsStrongDamageOverTime(statusType) || statusType == StatusType.BonusChange)
            {
                if (hasBonuses == false || bonuses.Count == 0)
                {
                    _logger.LogError($"[Config]: Status bonuses missing, statusType = {statusType}, raw = {parameters}");

                    return false;
                }
            }

            parsed = new StatusParameters(damageRatio, damageLength, maxStacks, bonuses);

            return true;
        }

        private char ResolveBonusListSeparator(StatusType statusType)
        {
            if (statusType == StatusType.BurningStrong || statusType == StatusType.PoisonStrong)
                return SeparatorFormat.PairSeparator;

            if (statusType == StatusType.BonusChange)
                return SeparatorFormat.ListSeparator;

            return (char)0;
        }

        private bool TryParseBonusList(
            string value,
            char listSeparator,
            StatusType statusType,
            string rawParameters,
            out IReadOnlyList<RewardBonus> bonuses)
        {
            bonuses = Array.Empty<RewardBonus>();

            if (string.IsNullOrWhiteSpace(value))
            {
                _logger.LogError($"[Config]: Status bonuses empty, statusType = {statusType}, raw = {rawParameters}");

                return false;
            }

            var parts = SplitTopLevel(value, listSeparator);
            var result = new List<RewardBonus>();

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                if (TryParseBonusTriple(part, out var bonus) == false)
                {
                    _logger.LogError($"[Config]: Status bonus triple invalid, statusType = {statusType}, part = {part}, raw = {rawParameters}");

                    return false;
                }

                result.Add(bonus);
            }

            if (result.Count == 0)
            {
                _logger.LogError($"[Config]: Status bonuses parsed empty, statusType = {statusType}, raw = {rawParameters}");

                return false;
            }

            bonuses = result;

            return true;
        }

        private bool TryParseBonusTriple(string part, out RewardBonus bonus)
        {
            bonus = default;

            var first = part.IndexOf(SeparatorFormat.KeyValueSeparator);

            if (first < 0)
                return false;

            var typeSpan = part.AsSpan(0, first).Trim();
            var rest = part.AsSpan(first + 1);
            var second = rest.IndexOf(SeparatorFormat.KeyValueSeparator);

            if (second < 0)
                return false;

            var idSpan = rest.Slice(0, second).Trim();
            var countSpan = rest.Slice(second + 1).Trim();

            if (typeSpan.Equals("bonus", StringComparison.OrdinalIgnoreCase) == false)
                return false;

            if (int.TryParse(idSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) == false)
                return false;

            if (id <= 0)
                return false;

            if (int.TryParse(countSpan, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) == false)
                return false;

            if (count <= 0)
                return false;

            bonus = new RewardBonus(id, count);

            return true;
        }

        private bool TryParseFloat(string value, out float parsed)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
        }

        private bool TryParseInt(string value, out int parsed)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
        }

        private bool IsDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.Burning
                || statusType == StatusType.BurningStrong
                || statusType == StatusType.Poison
                || statusType == StatusType.PoisonStrong;
        }

        private bool IsStrongDamageOverTime(StatusType statusType)
        {
            return statusType == StatusType.BurningStrong
                || statusType == StatusType.PoisonStrong;
        }

        private string UnwrapBrackets(ReadOnlySpan<char> value)
        {
            value = value.Trim();

            if (value.Length < 2)
                return value.ToString();

            if (value[0] == '[' && value[value.Length - 1] == ']')
                value = value.Slice(1, value.Length - 2).Trim();

            return value.ToString();
        }

        private List<string> SplitTopLevel(string input, char separator)
        {
            var result = new List<string>();

            if (string.IsNullOrWhiteSpace(input))
                return result;

            var depth = 0;
            var start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                var character = input[i];

                if (character == '[')
                {
                    depth += 1;
                    continue;
                }

                if (character == ']')
                {
                    if (0 < depth)
                        depth -= 1;

                    continue;
                }

                if (character != separator || 0 < depth)
                    continue;

                result.Add(input.Substring(start, i - start));
                start = i + 1;
            }

            if (start <= input.Length)
                result.Add(input.Substring(start));

            return result;
        }
    }
}

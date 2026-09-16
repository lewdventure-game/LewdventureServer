using Server.Configs;

namespace Server.Battles
{
    internal sealed class StatusParametersParser : IStatusParametersParser
    {
        private readonly IBattleRewardParser _battleRewardParser;

        public StatusParametersParser(IBattleRewardParser battleRewardParser)
        {
            _battleRewardParser = battleRewardParser;
        }

        public StatusParameters Parse(string parameters)
        {
            var pairs = SplitTopLevel(parameters, ';');
            var damageRatio = 0f;
            var damageLength = 0;
            var maxStacks = 1;
            IReadOnlyList<RewardBonus> bonuses = Array.Empty<RewardBonus>();

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i].AsSpan().Trim();

                if (pair.IsEmpty)
                    continue;

                var separator = pair.IndexOf(':');

                if (separator < 0)
                    continue;

                var key = pair.Slice(0, separator).Trim().ToString();
                var value = UnwrapBrackets(pair.Slice(separator + 1).Trim());

                switch (key)
                {
                    case "damage_ratio":
                        damageRatio = ParserUtils.GetFloat(value, 0f);
                        break;
                    case "damage_length":
                        damageLength = ParserUtils.GetInt(value, 0);
                        break;
                    case "max_stacks":
                        maxStacks = ParserUtils.GetInt(value, 1);
                        break;
                    case "bonuses":
                        bonuses = _battleRewardParser.ParseBonuses(value);
                        break;
                }
            }

            if (maxStacks < 1)
                maxStacks = 1;

            return new StatusParameters(damageRatio, damageLength, maxStacks, bonuses);
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

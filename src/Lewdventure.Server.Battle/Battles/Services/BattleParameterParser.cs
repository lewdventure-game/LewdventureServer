using Server.Configs;

namespace Server.Battles
{
    internal sealed class BattleParameterParser : IBattleParameterParser
    {
        public void ParseKeyValues(string parameters, Dictionary<string, string> output)
        {
            output.Clear();

            if (string.IsNullOrWhiteSpace(parameters))
                return;

            var pairs = SplitTopLevel(parameters, SeparatorFormat.PairSeparator);

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i].AsSpan().Trim();

                if (pair.IsEmpty)
                    continue;

                var separator = pair.IndexOf(SeparatorFormat.KeyValueSeparator);

                if (separator < 0)
                    continue;

                var key = pair.Slice(0, separator).Trim().ToString();
                var value = UnwrapBrackets(pair.Slice(separator + 1).Trim().ToString());

                if (key.Length == 0)
                    continue;

                output[key] = value;
            }
        }

        public string UnwrapBrackets(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var span = value.AsSpan().Trim();

            if (span.Length < 2)
                return span.ToString();

            if (span[0] == '[' && span[span.Length - 1] == ']')
                span = span.Slice(1, span.Length - 2).Trim();

            return span.ToString();
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

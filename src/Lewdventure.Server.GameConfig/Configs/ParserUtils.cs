using System.Globalization;

namespace Server.Configs
{
    internal sealed class ParserUtils
    {
        private readonly ICoreLog _coreLog;

        public ParserUtils(ICoreLog coreLog)
        {
            _coreLog = coreLog;
        }

        public void ParseToDictionary(string input, Dictionary<string, string> output)
        {
            if (string.IsNullOrEmpty(input))
                return;

            var pairs = SplitTopLevel(input, SeparatorFormat.PairSeparator);

            for (int i = 0; i < pairs.Count; i++)
            {
                var pair = pairs[i].AsSpan().Trim();

                if (pair.IsEmpty)
                    continue;

                var separator = pair.IndexOf(SeparatorFormat.KeyValueSeparator);

                if (separator < 0)
                {
                    _coreLog.Error($"[Error]: Invalid format '{pair.ToString()}'");

                    continue;
                }

                var keySpan = pair.Slice(0, separator).Trim();
                var value = UnwrapBrackets(pair.Slice(separator + 1).Trim().ToString());

                if (keySpan.IsEmpty)
                    continue;

                output[keySpan.ToString()] = value;
            }
        }

        public IReadOnlyList<int> ParseIntList(Dictionary<string, string> dictionary, string key)
        {
            if (dictionary.TryGetValue(key, out var value) == false || string.IsNullOrEmpty(value))
                return Array.Empty<int>();

            var span = value.AsSpan();
            var result = new List<int>();
            var start = 0;

            while (start < span.Length)
            {
                var end = span.Slice(start).IndexOf(SeparatorFormat.ListSeparator);

                if (end == -1)
                    end = span.Length - start;

                var partSpan = span.Slice(start, end).Trim();
                start += end + 1;

                if (partSpan.IsEmpty)
                    continue;

                if (int.TryParse(partSpan, out var id))
                    result.Add(id);
                else
                    _coreLog.Error($"[Error]: Failed to parse int '{partSpan.ToString()}' in key '{key}'");
            }

            return result;
        }

        public IReadOnlyList<int> ParseIntList(string value)
        {
            if (string.IsNullOrEmpty(value))
                return Array.Empty<int>();

            var span = value.AsSpan();
            var result = new List<int>();
            var start = 0;

            while (start < span.Length)
            {
                var end = span.Slice(start).IndexOf(SeparatorFormat.ListSeparator);

                if (end == -1)
                    end = span.Length - start;

                var partSpan = span.Slice(start, end).Trim();
                start += end + 1;

                if (partSpan.IsEmpty)
                    continue;

                if (int.TryParse(partSpan, out var id))
                    result.Add(id);
                else
                    _coreLog.Error($"[Error]: Failed to parse int '{partSpan.ToString()}'");
            }

            return result;
        }

        public string GetString(Dictionary<string, string> dictionary, string key, string defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) && string.IsNullOrEmpty(value) == false
                ? value
                : defaultValue;
        }

        public string GetString(string value, string defaultValue)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        public float GetFloat(Dictionary<string, string> dictionary, string key, float defaultValue)
        {
            if (dictionary.TryGetValue(key, out var value) == false)
                return defaultValue;

            return GetFloat(value, defaultValue);
        }

        public float GetFloat(string value, float defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            var trimmed = value.Trim();

            if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                return result;

            var normalized = trimmed.Replace(',', '.');

            if (float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
                return result;

            return defaultValue;
        }

        public int GetInt(Dictionary<string, string> dictionary, string key, int defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        public int GetInt(string value, int defaultValue)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        private string UnwrapBrackets(string value)
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

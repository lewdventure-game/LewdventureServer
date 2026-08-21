using System.Globalization;

namespace Server.Configs
{
    internal static class ParserUtils
    {
        internal static void ParseToDictionary(string input, Dictionary<string, string> output)
        {
            if (string.IsNullOrEmpty(input))
                return;

            var span = input.AsSpan();
            var start = 0;

            while (start < span.Length)
            {
                var pairEnd = span.Slice(start).IndexOf(SeparatorFormat.PairSeparator);

                if (pairEnd == -1)
                    pairEnd = span.Length - start;

                var pairSpan = span.Slice(start, pairEnd);
                start += pairEnd + 1;

                if (pairSpan.IsEmpty)
                    continue;

                var separator = pairSpan.IndexOf(SeparatorFormat.KeyValueSeparator);

                if (separator == -1)
                {
                    Console.WriteLine($"[Error]: Invalid format '{pairSpan.ToString()}'");

                    continue;
                }

                var keySpan = pairSpan.Slice(0, separator).Trim();
                var valueSpan = pairSpan.Slice(separator + 1).Trim();

                if (keySpan.IsEmpty == false)
                    output[keySpan.ToString()] = valueSpan.ToString();
            }
        }

        internal static IReadOnlyList<int> ParseIntList(Dictionary<string, string> dictionary, string key)
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
                    Console.WriteLine($"[Error]: Failed to parse int '{partSpan.ToString()}' in key '{key}'");
            }

            return result;
        }

        internal static IReadOnlyList<int> ParseIntList(string value)
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
                    Console.WriteLine($"[Error]: Failed to parse int '{partSpan.ToString()}'");
            }

            return result;
        }

        internal static string GetString(Dictionary<string, string> dictionary, string key, string defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) && string.IsNullOrEmpty(value) == false
                ? value
                : defaultValue;
        }

        internal static string GetString(string value, string defaultValue)
        {
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }

        internal static float GetFloat(Dictionary<string, string> dictionary, string key, float defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        internal static float GetFloat(string value, float defaultValue)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        internal static int GetInt(Dictionary<string, string> dictionary, string key, int defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }

        internal static int GetInt(string value, int defaultValue)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
                ? result
                : defaultValue;
        }
    }
}

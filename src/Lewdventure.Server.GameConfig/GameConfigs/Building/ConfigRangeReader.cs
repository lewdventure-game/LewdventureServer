using System.Text.RegularExpressions;

namespace Server.GameConfigs
{
    internal sealed class ConfigRangeReader
    {
        private const int DefaultStartRow = 1;

        private readonly Regex _startCell = new(@"^[A-Za-z]*(\d+)", RegexOptions.Compiled);

        public int ResolveStartRow(string range)
        {
            if (string.IsNullOrWhiteSpace(range))
                return DefaultStartRow;

            var start = range.Split(':')[0];
            var separator = start.LastIndexOf('!');

            if (0 <= separator)
                start = start.Substring(separator + 1);

            var match = _startCell.Match(start);

            if (match.Success == false)
                return DefaultStartRow;

            if (int.TryParse(match.Groups[1].Value, out var row) == false || row <= 0)
                return DefaultStartRow;

            return row;
        }
    }
}

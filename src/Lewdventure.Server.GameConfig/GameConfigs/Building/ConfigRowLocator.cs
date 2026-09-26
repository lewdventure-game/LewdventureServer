using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.GameConfigs
{
    internal sealed class ConfigRowLocator
    {
        private const string IdProperty = "id";

        private readonly ConfigRangeReader _configRangeReader;
        private readonly Regex _pathSuffix = new(@"\s*Path '[^']*', line \d+, position \d+\.?$", RegexOptions.Compiled);
        private readonly Regex _rowPrefix = new(@"^\[\d+\]\.?", RegexOptions.Compiled);
        private readonly Regex _conversion = new(@"^Error converting value (.+) to type '([^']+)'\.?$", RegexOptions.Compiled);
        private readonly Regex _plainConversion = new(@"^Could not convert \w+ to (\w+): (.+)\.$", RegexOptions.Compiled);

        public ConfigRowLocator(ConfigRangeReader configRangeReader)
        {
            _configRangeReader = configRangeReader;
        }

        public string Describe(ConfigSnapshotDomain domain, int rowIndex, JToken row, JsonException exception)
        {
            var address = $"Лист {domain.Domain}, {DescribeRow(domain, rowIndex)}{DescribeId(row)}";
            var column = ResolveColumn(exception);
            var reason = DescribeReason(exception);

            if (string.IsNullOrEmpty(column))
                return $"{address}: {reason}";

            return $"{address}, колонка {column}: {reason} Значение: {DescribeValue(row, column)}.";
        }

        private string DescribeReason(JsonException exception)
        {
            var message = _pathSuffix.Replace(exception.Message, string.Empty).Trim();
            var match = _conversion.Match(message);

            if (match.Success)
                return $"значение не подходит колонке, ожидался тип {ShortType(match.Groups[2].Value)}.";

            var plain = _plainConversion.Match(message);

            if (plain.Success)
                return $"значение не подходит колонке, ожидался тип {plain.Groups[1].Value}.";

            return message;
        }

        private string ShortType(string type)
        {
            var separator = type.LastIndexOf('.');

            if (separator < 0)
                return type;

            return type.Substring(separator + 1);
        }

        private string DescribeRow(ConfigSnapshotDomain domain, int rowIndex)
        {
            if (rowIndex < domain.SourceRows.Count)
                return $"строка {domain.SourceRows[rowIndex]}";

            return $"строка {_configRangeReader.ResolveStartRow(domain.Range) + rowIndex + 1}";
        }

        private string DescribeId(JToken row)
        {
            if (row is JObject rowObject == false)
                return string.Empty;

            var id = rowObject[IdProperty];

            if (id == null || id.Type == JTokenType.Null)
                return string.Empty;

            return $" (id {id})";
        }

        private string DescribeValue(JToken row, string column)
        {
            if (row is JObject rowObject == false)
                return "нет";

            var value = rowObject[column];

            if (value == null || value.Type == JTokenType.Null)
                return "пусто";

            return $"'{value}'";
        }

        private string ResolveColumn(JsonException exception)
        {
            if (exception is JsonSerializationException serialization)
                return TrimRowPrefix(serialization.Path);

            if (exception is JsonReaderException reader)
                return TrimRowPrefix(reader.Path);

            return string.Empty;
        }

        private string TrimRowPrefix(string? path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return _rowPrefix.Replace(path, string.Empty);
        }
    }
}

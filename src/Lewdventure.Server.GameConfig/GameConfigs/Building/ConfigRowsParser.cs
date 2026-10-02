using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Server.GameConfigs
{
    internal sealed class ConfigRowsParser
    {
        private const string NameColumn = "name";
        private const string ServiceColumn = "is_off";

        private readonly ConfigRowLocator _configRowLocator;
        private readonly JsonSerializer _serializer;

        public ConfigRowsParser(ConfigRowLocator configRowLocator)
        {
            _configRowLocator = configRowLocator;
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new StringEnumConverter(new SnakeCaseNamingStrategy()) },
            });
        }

        public List<T> Parse<T>(ConfigSnapshotDomain domain, List<string> errors)
            where T : class
        {
            var parsed = new List<T>();
            JArray rows;

            try
            {
                rows = JArray.Parse(domain.RowsJson);
            }
            catch (JsonException exception)
            {
                errors.Add($"Лист {domain.Domain}: строки не читаются как JSON: {exception.Message}");

                return parsed;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (IsEmptyRow(rows[i]))
                    continue;

                try
                {
                    var item = rows[i].ToObject<T>(_serializer);

                    if (item != null)
                        parsed.Add(item);
                }
                catch (JsonException exception)
                {
                    errors.Add(_configRowLocator.Describe(domain, i, rows[i], exception));
                }
            }

            return parsed;
        }

        private bool IsEmptyRow(JToken row)
        {
            if (row is JObject rowObject == false)
                return false;

            foreach (var property in rowObject.Properties())
            {
                if (string.Equals(property.Name, NameColumn, StringComparison.Ordinal))
                    continue;

                if (string.Equals(property.Name, ServiceColumn, StringComparison.Ordinal))
                    continue;

                var value = property.Value;

                if (value == null || value.Type == JTokenType.Null)
                    continue;

                if (value.Type == JTokenType.String && value.ToString().Trim().Length == 0)
                    continue;

                return false;
            }

            return true;
        }
    }
}

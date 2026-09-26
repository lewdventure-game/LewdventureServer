using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Server.GameConfigs
{
    internal sealed class ConfigRowsParser
    {
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
    }
}

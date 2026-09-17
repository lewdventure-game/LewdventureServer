using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Server.GameConfigs
{
    internal sealed class ConfigRowsParser
    {
        private readonly JsonSerializerSettings _settings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new StringEnumConverter(new SnakeCaseNamingStrategy()) },
        };

        public List<T> Parse<T>(string rowsJson)
            where T : class
        {
            var result = JsonConvert.DeserializeObject<List<T>>(rowsJson, _settings);

            if (result == null)
                return new List<T>();

            return result;
        }
    }
}

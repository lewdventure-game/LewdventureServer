using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests.Golden.Infrastructure
{
    internal sealed class ConfigSnapshotLoader
    {
        private readonly JsonSerializerSettings _settings = new()
        {
            DateParseHandling = DateParseHandling.None,
        };

        public ConfigSnapshotFile Load(string path)
        {
            var text = File.ReadAllText(path);
            var snapshot = JsonConvert.DeserializeObject<ConfigSnapshotFile>(text, _settings);

            if (snapshot == null)
                throw new InvalidOperationException($"[Golden] config snapshot is empty path = {path}");

            return snapshot;
        }

        public void Save(string path, ConfigSnapshotFile snapshot)
        {
            var directory = Path.GetDirectoryName(path);

            if (string.IsNullOrEmpty(directory) == false)
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, JsonConvert.SerializeObject(snapshot, Formatting.Indented, _settings));
        }

        public JArray ParseRows(string rowsJson)
        {
            using var stringReader = new StringReader(rowsJson);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
            };

            return JArray.Load(jsonReader);
        }
    }
}

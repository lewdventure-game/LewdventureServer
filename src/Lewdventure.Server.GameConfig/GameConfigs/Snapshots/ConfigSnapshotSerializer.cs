using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotSerializer
    {
        private const string DateFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        private readonly ConfigSnapshotHasher _hasher;
        private readonly JsonSerializerSettings _settings = new()
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Decimal,
        };

        public ConfigSnapshotSerializer(ConfigSnapshotHasher hasher)
        {
            _hasher = hasher;
        }

        public GameConfigSnapshot Deserialize(string text)
        {
            var model = JsonConvert.DeserializeObject<ConfigSnapshotFileModel>(text, _settings);

            if (model == null)
                throw new InvalidDataException("Config snapshot file is empty.");

            if (string.Equals(model.Format, ConfigSnapshotFileModel.FormatName, StringComparison.Ordinal) == false)
                throw new InvalidDataException($"Config snapshot format {model.Format} is not supported.");

            if (model.FormatVersion != GameConfigSnapshot.CurrentFormatVersion)
                throw new InvalidDataException($"Config snapshot format version {model.FormatVersion} is not supported.");

            var domains = new List<ConfigSnapshotDomain>(model.Domains.Count);

            for (int i = 0; i < model.Domains.Count; i++)
            {
                var domain = model.Domains[i];

                domains.Add(new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, domain.Rows.ToString(Formatting.None)));
            }

            var version = _hasher.ComputeVersion(domains);

            if (string.IsNullOrEmpty(model.Version) == false && string.Equals(model.Version, version, StringComparison.Ordinal) == false)
                throw new InvalidDataException($"Config snapshot version mismatch declared = {model.Version} computed = {version}.");

            return new GameConfigSnapshot(version, ParseDate(model.CreatedAt), model.SourceKind, domains);
        }

        public string Serialize(GameConfigSnapshot snapshot)
        {
            var model = new ConfigSnapshotFileModel
            {
                Version = snapshot.Version,
                CreatedAt = snapshot.CreatedAt.ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture),
                SourceKind = snapshot.SourceKind,
            };

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                model.Domains.Add(new ConfigSnapshotFileDomain
                {
                    Domain = domain.Domain,
                    SpreadsheetId = domain.SpreadsheetId,
                    Range = domain.Range,
                    Rows = ParseRows(domain.RowsJson),
                });
            }

            return JsonConvert.SerializeObject(model, Formatting.Indented, _settings);
        }

        private JArray ParseRows(string rowsJson)
        {
            using var stringReader = new StringReader(rowsJson);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
            };

            return JArray.Load(jsonReader);
        }

        private DateTime ParseDate(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                return date;

            return DateTime.MinValue;
        }
    }
}

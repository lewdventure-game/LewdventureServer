namespace Server.GameConfigs
{
    internal sealed class UploadedSheetsSnapshotBuilder
    {
        public const string SourceKind = "SheetsUpload";

        private readonly ConfigDomainNames _configDomainNames;
        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly SheetRowsConverter _sheetRowsConverter;

        public UploadedSheetsSnapshotBuilder(
            ConfigDomainNames configDomainNames,
            ConfigSnapshotHasher configSnapshotHasher,
            SheetRowsConverter sheetRowsConverter)
        {
            _configDomainNames = configDomainNames;
            _configSnapshotHasher = configSnapshotHasher;
            _sheetRowsConverter = sheetRowsConverter;
        }

        public bool TryBuild(IReadOnlyList<UploadedSheet> sheets, out GameConfigSnapshot snapshot, out List<string> errors)
        {
            snapshot = null!;
            errors = new List<string>();

            var domainNames = _configDomainNames.Ordered;
            var domains = new List<ConfigSnapshotDomain>(domainNames.Count);

            for (int i = 0; i < domainNames.Count; i++)
            {
                var domainName = domainNames[i];
                var sheet = Find(sheets, domainName);

                if (sheet == null)
                {
                    errors.Add($"Sheet for domain {domainName} is missing in the payload.");

                    continue;
                }

                if (string.IsNullOrWhiteSpace(sheet.SpreadsheetId) || string.IsNullOrWhiteSpace(sheet.Range))
                {
                    errors.Add($"Sheet {domainName} must define spreadsheetId and range.");

                    continue;
                }

                domains.Add(new ConfigSnapshotDomain(sheet.Domain, sheet.SpreadsheetId, sheet.Range, _sheetRowsConverter.ToRowsJson(sheet.Values)));
            }

            if (0 < errors.Count)
                return false;

            snapshot = new GameConfigSnapshot(_configSnapshotHasher.ComputeVersion(domains), DateTime.UtcNow, SourceKind, domains);

            return true;
        }

        private UploadedSheet? Find(IReadOnlyList<UploadedSheet> sheets, string domain)
        {
            for (int i = 0; i < sheets.Count; i++)
            {
                if (string.Equals(sheets[i].Domain, domain, StringComparison.OrdinalIgnoreCase))
                    return sheets[i];
            }

            return null;
        }
    }
}

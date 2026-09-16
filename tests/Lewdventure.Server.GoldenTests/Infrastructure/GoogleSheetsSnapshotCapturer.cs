using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Newtonsoft.Json;

namespace Tests.Golden.Infrastructure
{
    internal sealed class GoogleSheetsSnapshotCapturer
    {
        private readonly ConfigSheetCatalog _configSheetCatalog;
        private readonly ConfigSnapshotLoader _configSnapshotLoader;
        private readonly SheetRowsBuilder _sheetRowsBuilder;

        public GoogleSheetsSnapshotCapturer(
            ConfigSheetCatalog configSheetCatalog,
            ConfigSnapshotLoader configSnapshotLoader,
            SheetRowsBuilder sheetRowsBuilder)
        {
            _configSheetCatalog = configSheetCatalog;
            _configSnapshotLoader = configSnapshotLoader;
            _sheetRowsBuilder = sheetRowsBuilder;
        }

        public async Task<ConfigSnapshotFile> CaptureAsync(string credentialsPath)
        {
            var credential = CredentialFactory.FromFile<ServiceAccountCredential>(credentialsPath)
                .ToGoogleCredential()
                .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

            using var sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "GameConfigReader",
            });

            var snapshot = new ConfigSnapshotFile
            {
                Format = "lewdventure-config-snapshot",
                FormatVersion = 1,
                CapturedAt = DateTime.UtcNow,
            };

            var sheets = _configSheetCatalog.Sheets;

            for (int i = 0; i < sheets.Count; i++)
            {
                var sheet = sheets[i];
                var response = await sheetsService.Spreadsheets.Values.Get(sheet.SpreadsheetId, sheet.Range).ExecuteAsync();
                var rowsJson = _sheetRowsBuilder.Build(response.Values);
                var rows = _configSnapshotLoader.ParseRows(rowsJson);

                if (string.Equals(rows.ToString(Formatting.None), rowsJson, StringComparison.Ordinal) == false)
                    throw new InvalidOperationException($"[Golden] rows round-trip mismatch domain = {sheet.Domain}");

                snapshot.Domains.Add(new ConfigSnapshotDomain
                {
                    Domain = sheet.Domain,
                    SpreadsheetId = sheet.SpreadsheetId,
                    Range = sheet.Range,
                    Rows = rows,
                });

                await Task.Delay(150);
            }

            return snapshot;
        }
    }
}

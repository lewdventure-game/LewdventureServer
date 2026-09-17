using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Server.GameConfigs;

namespace Server.Infrastructure.GoogleSheets
{
    internal sealed class GoogleSheetsConfigImporter : IDisposable
    {
        public const string SourceKind = "GoogleSheets";

        private readonly ConfigDomainNames _configDomainNames;
        private readonly ConfigSnapshotHasher _configSnapshotHasher;
        private readonly GoogleCredentialProvider _googleCredentialProvider;
        private readonly ILogger<GoogleSheetsConfigImporter> _logger;
        private readonly GoogleSheetsOptions _options;
        private readonly SemaphoreSlim _serviceLock = new(1, 1);

        private SheetsService? _sheetsService;

        public GoogleSheetsConfigImporter(
            ConfigDomainNames configDomainNames,
            ConfigSnapshotHasher configSnapshotHasher,
            GoogleCredentialProvider googleCredentialProvider,
            ILogger<GoogleSheetsConfigImporter> logger,
            IOptions<GoogleSheetsOptions> options)
        {
            _configDomainNames = configDomainNames;
            _configSnapshotHasher = configSnapshotHasher;
            _googleCredentialProvider = googleCredentialProvider;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<GameConfigSnapshot> ImportAsync(CancellationToken cancellationToken)
        {
            var sheetsService = await GetSheetsServiceAsync();
            var request = sheetsService.Spreadsheets.Values;
            var domainNames = _configDomainNames.Ordered;
            var domains = new List<ConfigSnapshotDomain>(domainNames.Count);

            for (int i = 0; i < domainNames.Count; i++)
            {
                if (0 < i)
                    await Task.Delay(_options.DelayBetweenSheetsMs, cancellationToken);

                var sheet = GetSheet(domainNames[i]);
                var rowsJson = await DownloadWithRetryAsync(request, sheet, cancellationToken);

                domains.Add(new ConfigSnapshotDomain(sheet.Domain, sheet.SpreadsheetId, sheet.Range, rowsJson));
            }

            var version = _configSnapshotHasher.ComputeVersion(domains);

            _logger.LogInformation("[Config][Snapshot] imported from Google Sheets version = {Version}", version);

            return new GameConfigSnapshot(version, DateTime.UtcNow, SourceKind, domains);
        }

        public void Dispose()
        {
            _sheetsService?.Dispose();
            _serviceLock.Dispose();
        }

        private async Task<SheetsService> GetSheetsServiceAsync()
        {
            await _serviceLock.WaitAsync();

            try
            {
                if (_sheetsService == null)
                {
                    var credential = _googleCredentialProvider.Create(_options);

                    _sheetsService = new SheetsService(new BaseClientService.Initializer()
                    {
                        HttpClientInitializer = credential,
                        ApplicationName = _options.ApplicationName
                    });
                }

                return _sheetsService;
            }
            finally
            {
                _serviceLock.Release();
            }
        }

        private GoogleSheetDefinition GetSheet(string domain)
        {
            for (int i = 0; i < _options.Sheets.Count; i++)
            {
                if (string.Equals(_options.Sheets[i].Domain, domain, StringComparison.Ordinal))
                    return _options.Sheets[i];
            }

            throw new InvalidOperationException($"GoogleSheets sheet for domain {domain} is not configured.");
        }

        private async Task<string> DownloadWithRetryAsync(
            SpreadsheetsResource.ValuesResource request,
            GoogleSheetDefinition sheet,
            CancellationToken cancellationToken)
        {
            var maxRetries = _options.MaxRetries;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug($"[Config] download start sheet = {sheet.Domain} attempt = {attempt}/{maxRetries}");

                    return await DownloadRowsAsync(request, sheet, cancellationToken);
                }
                catch (Exception exception) when (attempt < maxRetries && cancellationToken.IsCancellationRequested == false)
                {
                    var delayMs = attempt * 1000;

                    _logger.LogWarning($"[Config] download retry sheet = {sheet.Domain} attempt = {attempt} delayMs = {delayMs} error = {exception.Message}");

                    await Task.Delay(delayMs, cancellationToken);
                }
            }

            throw new Exception($"Не удалось скачать таблицу '{sheet.Domain}' после {maxRetries} попыток.");
        }

        private async Task<string> DownloadRowsAsync(
            SpreadsheetsResource.ValuesResource request,
            GoogleSheetDefinition sheet,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation($"[Config] downloading sheet = {sheet.Domain} range = {sheet.Range}");

            var response = await request.Get(sheet.SpreadsheetId, sheet.Range).ExecuteAsync(cancellationToken);
            var values = response.Values;
            var rowsData = new List<Dictionary<string, object>>();

            if (values == null || values.Count < 2)
            {
                _logger.LogWarning($"[Config] sheet empty sheet = {sheet.Domain}");

                return JsonConvert.SerializeObject(rowsData);
            }

            var headers = new List<string>(values[0].Count);

            for (int headerIndex = 0; headerIndex < values[0].Count; headerIndex++)
            {
                var header = values[0][headerIndex];
                var headerText = header == null ? string.Empty : header.ToString();

                headers.Add(headerText == null ? string.Empty : headerText.Trim());
            }

            for (int i = 1; i < values.Count; i++)
            {
                var row = values[i];
                var rowDict = new Dictionary<string, object>();
                var isActive = true;

                for (int j = 0; j < headers.Count && j < row.Count; j++)
                {
                    var header = headers[j];

                    if (string.IsNullOrEmpty(header))
                        continue;

                    var cellValue = row[j];

                    if (cellValue is string stringValue
                        && (stringValue.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
                            || stringValue.Equals("FALSE", StringComparison.OrdinalIgnoreCase)))
                    {
                        cellValue = bool.Parse(stringValue);
                    }

                    rowDict[header] = cellValue ?? string.Empty;

                    if (header.Equals("is_off", StringComparison.OrdinalIgnoreCase) && cellValue is bool isOff && isOff)
                        isActive = false;
                }

                if (isActive)
                    rowsData.Add(rowDict);
            }

            _logger.LogInformation($"[Config] downloaded sheet = {sheet.Domain} count = {rowsData.Count}");

            return JsonConvert.SerializeObject(rowsData);
        }
    }
}

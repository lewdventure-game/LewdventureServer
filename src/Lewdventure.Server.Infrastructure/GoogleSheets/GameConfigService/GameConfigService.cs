using Core.Collections;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Server.Bonuses;
using Server.Configs;
using Server.Entities;
using Server.Infrastructure.GoogleSheets;
using Server.Equipments;
using Server.Perks;
using Server.Statuses;
using Server.Stories;

namespace Server.Services
{
    internal sealed class GameConfigService : IGameConfigService
    {
        private readonly ILogger<IGameConfigService> _logger;
        private readonly IBonusWorkModeParser _bonusWorkModeParser;
        private readonly IConfigDistributor _configDistributor;

        private readonly GoogleSheetsOptions _options;
        private readonly SheetsService _sheetsService;
        private readonly ReaderWriterLockSlim _cacheLock = new();

        public GameConfigService(
            ILogger<IGameConfigService> logger,
            IBonusWorkModeParser bonusWorkModeParser,
            IConfigDistributor configDistributor,
            IOptions<GoogleSheetsOptions> options,
            GoogleCredentialProvider googleCredentialProvider)
        {
            _logger = logger;
            _bonusWorkModeParser = bonusWorkModeParser;
            _configDistributor = configDistributor;
            _options = options.Value;

            var credential = googleCredentialProvider.Create(_options);

            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = _options.ApplicationName
            });
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateAllConfigsAsync(bool isDevEnvironment)
        {
            var request = _sheetsService.Spreadsheets.Values;

            try
            {
                var tempConstants = await DownloadWithRetryAsync<ConstantsMapper>(request, GetSheet(GoogleSheetDomains.Constants));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempCharacters = await DownloadWithRetryAsync<CharacterMapper>(request, GetSheet(GoogleSheetDomains.Characters));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempBonuses = await DownloadWithRetryAsync<BonusMapper>(request, GetSheet(GoogleSheetDomains.Bonuses));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempStatuses = await DownloadWithRetryAsync<StatusMapper>(request, GetSheet(GoogleSheetDomains.Statuses));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempSummons = await DownloadWithRetryAsync<SummonMapper>(request, GetSheet(GoogleSheetDomains.Summons));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempSummonLevels = await DownloadWithRetryAsync<SummonLevelMapper>(request, GetSheet(GoogleSheetDomains.SummonLevels));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempMasteries = await DownloadWithRetryAsync<MasteryMapper>(request, GetSheet(GoogleSheetDomains.Mastery));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempEnemies = await DownloadWithRetryAsync<EnemyMapper>(request, GetSheet(GoogleSheetDomains.Enemies));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempEquipments = await DownloadWithRetryAsync<EquipmentMapper>(request, GetSheet(GoogleSheetDomains.Equipments));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempStoryLevels = await DownloadWithRetryAsync<StoryLevelMapper>(request, GetSheet(GoogleSheetDomains.StoryLevels));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempStoryStages = await DownloadWithRetryAsync<StoryStageMapper>(request, GetSheet(GoogleSheetDomains.StoryStages));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempStoryEvents = await DownloadWithRetryAsync<StoryEventMapper>(request, GetSheet(GoogleSheetDomains.StoryEvents));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempExpPatterns = await DownloadWithRetryAsync<ExperienceLevelPatternMapper>(request, GetSheet(GoogleSheetDomains.ExpLevelsPatterns));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempPerks = await DownloadWithRetryAsync<PerkMapper>(request, GetSheet(GoogleSheetDomains.Perks));

                await Task.Delay(_options.DelayBetweenSheetsMs);

                var tempPerkGroups = await DownloadWithRetryAsync<PerkGroupMapper>(request, GetSheet(GoogleSheetDomains.PerkGroups));

                _cacheLock.EnterWriteLock();

                try
                {
                    _configDistributor.ClearAll();

                    AddList(_configDistributor.Constants, tempConstants, "Constants");
                    AddList(_configDistributor.Characters, tempCharacters, "Characters");
                    AddBonuses(tempBonuses);
                    AddList(_configDistributor.Statuses, tempStatuses, "Statuses");
                    AddList(_configDistributor.Summons, tempSummons, "Summons");
                    AddList(_configDistributor.SummonLevels, tempSummonLevels, "Summon_levels");
                    AddList(_configDistributor.Masteries, tempMasteries, "Mastery");
                    AddList(_configDistributor.Enemies, tempEnemies, "Enemies");
                    AddList(_configDistributor.Equipments, tempEquipments, "Equipments");
                    AddList(_configDistributor.StoryLevels, tempStoryLevels, "Story_levels");
                    AddList(_configDistributor.StoryStages, tempStoryStages, "Story_stages");
                    AddList(_configDistributor.StoryEvents, tempStoryEvents, "Story_events");
                    AddList(_configDistributor.ExperienceLevelPatterns, tempExpPatterns, "Exp_levels_patterns");
                    AddList(_configDistributor.Perks, tempPerks, "Perks");
                    AddList(_configDistributor.PerkGroups, tempPerkGroups, "Perk_groups");

                    // Trainings / Artifacts / Aspects: sheet ids not wired yet — managers stay empty after ClearAll.
                    _logger.LogInformation($"[Config] Trainings stub empty; sheet id not wired count = {_configDistributor.Trainings.Collection.Count}");
                    _logger.LogInformation($"[Config] Artifacts stub empty; sheet id not wired count = {_configDistributor.Artifacts.Collection.Count}");
                    _logger.LogInformation($"[Config] Aspects stub empty; sheet id not wired count = {_configDistributor.Aspects.Collection.Count}");

                    _logger.LogInformation($"[Config] inventory summary bonuses = {_configDistributor.Bonuses.Count}, statuses = {_configDistributor.Statuses.Collection.Count}, perks = {_configDistributor.Perks.Collection.Count}, perkGroups = {_configDistributor.PerkGroups.Collection.Count}, trainings = {_configDistributor.Trainings.Collection.Count}, artifacts = {_configDistributor.Artifacts.Collection.Count}, aspects = {_configDistributor.Aspects.Collection.Count}");
                    _logger.LogDebug($"[FIX][Config] inventory summary after sync bonuses = {_configDistributor.Bonuses.Count}, statuses = {_configDistributor.Statuses.Collection.Count}, perks = {_configDistributor.Perks.Collection.Count}");

                    return (true, "Конфиги успешно обновлены");
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"[Config] update failed; keeping previous configs message = {exception.Message}");

                return (false, $"Ошибка обновления: {exception.Message}");
            }
        }

        private void AddBonuses(List<BonusMapper> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var workMode = _bonusWorkModeParser.Parse(item.WorkModeParameters);

                if (_configDistributor.Bonuses.Add(item.Id, item) == false)
                {
                    _logger.LogWarning($"[Config] duplicate bonus id = {item.Id}; skipped");

                    continue;
                }

                _logger.LogDebug($"[Config] bonus loaded id = {item.Id} type = {item.BonusType} workMode = {workMode.Kind}");
            }

            _logger.LogInformation($"[Config] Bonuses count = {_configDistributor.Bonuses.Count}");
        }

        private void AddList<TMapper, TInterface>(
            IManager<TInterface> manager,
            List<TMapper> items,
            string sheetName)
            where TMapper : class, TInterface
            where TInterface : class
        {
            for (int i = 0; i < items.Count; i++)
                manager.Add(items[i]);

            _logger.LogInformation($"[Config] {sheetName} count = {manager.Collection.Count}");
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

        private async Task<List<T>> DownloadWithRetryAsync<T>(
            SpreadsheetsResource.ValuesResource request,
            GoogleSheetDefinition sheet)
            where T : class, IConfigMapper, new()
        {
            var maxRetries = _options.MaxRetries;
            var sheetId = sheet.SpreadsheetId;
            var range = sheet.Range;
            var sheetName = sheet.Domain;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogDebug($"[Config] download start sheet = {sheetName} attempt = {attempt}/{maxRetries}");

                    return await DownloadAndParseAsync<T>(request, sheetId, range, sheetName);
                }
                catch (Exception exception) when (attempt < maxRetries)
                {
                    var delayMs = attempt * 1000;

                    _logger.LogWarning($"[Config] download retry sheet = {sheetName} attempt = {attempt} delayMs = {delayMs} error = {exception.Message}");

                    await Task.Delay(delayMs);
                }
            }

            throw new Exception($"Не удалось скачать таблицу '{sheetName}' после {maxRetries} попыток.");
        }

        private async Task<List<T>> DownloadAndParseAsync<T>(
            SpreadsheetsResource.ValuesResource request,
            string sheetId,
            string range,
            string sheetName)
            where T : class
        {
            _logger.LogInformation($"[Config] downloading sheet = {sheetName} range = {range}");

            var response = await request.Get(sheetId, range).ExecuteAsync();
            var values = response.Values;

            if (values == null || values.Count < 2)
            {
                _logger.LogWarning($"[Config] sheet empty sheet = {sheetName}");

                return new List<T>();
            }

            var headers = new List<string>(values[0].Count);

            for (int headerIndex = 0; headerIndex < values[0].Count; headerIndex++)
            {
                var header = values[0][headerIndex];
                var headerText = header == null ? string.Empty : header.ToString();

                headers.Add(headerText == null ? string.Empty : headerText.Trim());
            }

            var rowsData = new List<Dictionary<string, object>>();

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

            var jsonString = JsonConvert.SerializeObject(rowsData);

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore,
                Converters = { new StringEnumConverter(new SnakeCaseNamingStrategy()) },
            };

            var result = JsonConvert.DeserializeObject<List<T>>(jsonString, settings);

            if (result == null)
            {
                _logger.LogWarning($"[Config] deserialize failed sheet = {sheetName}");

                return new List<T>();
            }

            _logger.LogInformation($"[Config] downloaded sheet = {sheetName} count = {result.Count}");

            return result;
        }
    }
}

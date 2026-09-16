using Core.Collections;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Server.Bonuses;
using Server.Configs;
using Server.Entities;
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

        private readonly SheetsService _sheetsService;
        private readonly ReaderWriterLockSlim _cacheLock = new();

        public GameConfigService(
            ILogger<IGameConfigService> logger,
            IBonusWorkModeParser bonusWorkModeParser,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _bonusWorkModeParser = bonusWorkModeParser;
            _configDistributor = configDistributor;

            var credentialPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "google-credentials.json");

            if (File.Exists(credentialPath) == false)
                throw new FileNotFoundException("Файл google-credentials.json не найден. Убедитесь, что он добавлен в проект и имеет свойство 'Копировать, если новее'.");

            var credential = CredentialFactory.FromFile<ServiceAccountCredential>(credentialPath)
                .ToGoogleCredential()
                .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GameConfigReader"
            });
        }

        public async Task<(bool Success, string ErrorMessage)> UpdateAllConfigsAsync(bool isDevEnvironment)
        {
            var request = _sheetsService.Spreadsheets.Values;

            try
            {
                var tempConstants = await DownloadWithRetryAsync<ConstantsMapper>(request, "1LIw9xcJQmsn4GThnsLQITpRLJ_6avKv-wTBj0MpsVNM", "B:E", "Constants");

                await Task.Delay(150);

                var tempCharacters = await DownloadWithRetryAsync<CharacterMapper>(request, "1rB22U8FrboY1hHqg1AwhEvDBzb1OxK9JTUWZ_1isRTg", "B:J", "Characters");

                await Task.Delay(150);

                var tempBonuses = await DownloadWithRetryAsync<BonusMapper>(request, "1jrzVDp9dTRtJBjyFFP1adtkbcqMEHIfnWs3XsJuR2ac", "B:G", "Bonuses");

                await Task.Delay(150);

                var tempStatuses = await DownloadWithRetryAsync<StatusMapper>(request, "1Dwm4eRVQmLegaxulMk2RdNeOT7GlGu6QORQqeKnuUiY", "B:H", "Statuses");

                await Task.Delay(150);

                var tempSummons = await DownloadWithRetryAsync<SummonMapper>(request, "1QstDNh059XftqtZcIdIL3o80o_g8qQs_akKFe5jChCk", "B:J", "Summons");

                await Task.Delay(150);

                var tempSummonLevels = await DownloadWithRetryAsync<SummonLevelMapper>(request, "18swHo4NuLgys6_oGk_zqqpwadSm94KsBm-OoJIdoxR8", "B:I", "Summon_levels");

                await Task.Delay(150);

                var tempMasteries = await DownloadWithRetryAsync<MasteryMapper>(request, "1grplwUHMfdcs0-0QvFZvhQqsrcS4ywwe6nTQsEB6EOg", "B:H", "Mastery");

                await Task.Delay(150);

                var tempEnemies = await DownloadWithRetryAsync<EnemyMapper>(request, "1GLSin50lIGoTOZbmV3OQU8TXB_XAdUcnsnsfBK0AsKk", "B:Q", "Enemies");

                await Task.Delay(150);

                var tempEquipments = await DownloadWithRetryAsync<EquipmentMapper>(request, "1XP47_4sQ5uK2_6rGSBH4bkWgXVDBLfiYIYtVk9qIB0E", "B:R", "Equipments");

                await Task.Delay(150);

                var tempStoryLevels = await DownloadWithRetryAsync<StoryLevelMapper>(request, "1gz8t6fmWwIwvz93U7pKZ8rrBu9RdfyJB5Yn9G2ITuCE", "B:I", "Story_levels");

                await Task.Delay(150);

                var tempStoryStages = await DownloadWithRetryAsync<StoryStageMapper>(request, "1csDHVX7F0bStlHAayJV_3TOLOoyIbbCdg4O3ORTpx8g", "B:G", "Story_stages");

                await Task.Delay(150);

                var tempStoryEvents = await DownloadWithRetryAsync<StoryEventMapper>(request, "1chWPFzItT87MzdrEQAp7DvAkhEiY_xuLwor42fyFGDk", "B:G", "Story_events");

                await Task.Delay(150);

                var tempExpPatterns = await DownloadWithRetryAsync<ExperienceLevelPatternMapper>(request, "1YC9UR3RLOU-0r14ovkXeR_Dp1dtkkJCX_veZxLSBmVM", "B:G", "Exp_levels_patterns");

                await Task.Delay(150);

                var tempPerks = await DownloadWithRetryAsync<PerkMapper>(request, "1UyW7R_DZHDTiDtZoicZQ9GhDctVaK_zA7C0aBP0T6Hk", "B:J", "Perks");

                await Task.Delay(150);

                var tempPerkGroups = await DownloadWithRetryAsync<PerkGroupMapper>(request, "1USA6a-252oCKSmCqUDMtQn78pMiubIIj2garPm0pyBU", "B:G", "Perk_groups");

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

        private async Task<List<T>> DownloadWithRetryAsync<T>(
            SpreadsheetsResource.ValuesResource request,
            string sheetId,
            string range,
            string sheetName)
            where T : class, IConfigMapper, new()
        {
            var maxRetries = 3;

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

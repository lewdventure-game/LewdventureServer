using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Balance
{
    internal sealed class IndexModel : AdminPageModel
    {
        public const int SummonSlots = 3;
        public const int EquipmentSlots = 3;

        private const char TargetSeparator = '|';
        private const char EditSeparator = '|';
        private const int HistoryLimit = 30;
        private const int MaxChartPoints = 100;
        private const string CacheKeyPrefix = "balance:";
        private const string QueuedStatus = "queued";
        private const string RunningStatus = "running";
        private const string DoneStatus = "done";
        private const string FailedStatus = "failed";
        private const string ScenarioField = "scenario";
        private const int VersionsLifetimeSeconds = 30;
        private const int PinnedCatalogLifetimeMinutes = 30;

        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IMemoryCache _memoryCache;

        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient, IMemoryCache memoryCache)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
            _memoryCache = memoryCache;
        }

        [BindProperty(SupportsGet = true)]
        public string Version { get; set; } = string.Empty;

        [BindProperty]
        public int CharacterId { get; set; }

        [BindProperty]
        public int PromoteLevel { get; set; } = 1;

        [BindProperty]
        public float CurrentHealth { get; set; }

        [BindProperty]
        public List<BalanceSummonModel> Summons { get; set; } = new();

        [BindProperty]
        public List<BalanceEquipmentModel> Equipment { get; set; } = new();

        [BindProperty]
        public string AccountBonusesText { get; set; } = string.Empty;

        [BindProperty]
        public string RunPerksText { get; set; } = string.Empty;

        [BindProperty]
        public string RunBonusesText { get; set; } = string.Empty;

        [BindProperty]
        public string RunStatusesText { get; set; } = string.Empty;

        [BindProperty]
        public string Fight { get; set; } = string.Empty;

        [BindProperty]
        public string EnemiesText { get; set; } = string.Empty;

        [BindProperty]
        public ulong Seed { get; set; } = 1;

        [BindProperty]
        public int Battles { get; set; } = 200;

        [BindProperty]
        public string LoadUserId { get; set; } = string.Empty;

        [BindProperty]
        public int LevelId { get; set; }

        [BindProperty]
        public int LevelRuns { get; set; } = 100;

        [BindProperty]
        public string PerkPolicy { get; set; } = "first";

        [BindProperty]
        public string PerkPriorityText { get; set; } = string.Empty;

        [BindProperty]
        public string ForkPolicy { get; set; } = "firstButton";

        [BindProperty]
        public string EditsText { get; set; } = string.Empty;

        [BindProperty]
        public string VersionB { get; set; } = string.Empty;

        [BindProperty]
        public string EditsBText { get; set; } = string.Empty;

        [BindProperty]
        public string CompareKind { get; set; } = "series";

        [BindProperty]
        public string SweepKind { get; set; } = "series";

        [BindProperty]
        public string SweepSheet { get; set; } = string.Empty;

        [BindProperty]
        public string SweepRow { get; set; } = string.Empty;

        [BindProperty]
        public string SweepColumn { get; set; } = string.Empty;

        [BindProperty]
        public string SweepFrom { get; set; } = string.Empty;

        [BindProperty]
        public string SweepTo { get; set; } = string.Empty;

        [BindProperty]
        public string SweepStep { get; set; } = string.Empty;

        [BindProperty]
        public string ScenarioName { get; set; } = string.Empty;

        [BindProperty]
        public string ScenarioId { get; set; } = string.Empty;

        [BindProperty]
        public string HistoryId { get; set; } = string.Empty;

        [BindProperty]
        public int ProgressLevelId { get; set; }

        public List<BalanceVersionModel> Versions { get; private set; } = new();

        public BalanceCatalogModel Catalog { get; private set; } = new();

        public List<BalanceLayerModel> Layers { get; private set; } = new();

        public BalanceBattleResultModel? Battle { get; private set; }

        public BalanceSeriesResultModel? Series { get; private set; }

        public BalanceLevelResultModel? Level { get; private set; }

        public BalanceCompareResultModel? Compare { get; private set; }

        public BalanceSweepResultModel? Sweep { get; private set; }

        public List<BalanceScenarioModel> Scenarios { get; private set; } = new();

        public List<BalanceRunModel> History { get; private set; } = new();

        public List<string> EditErrors { get; } = new();

        public BalanceJobModel? Job { get; private set; }

        public BalanceProgressModel? Progress { get; private set; }

        public bool JobPending => Job != null && (Job.Status == QueuedStatus || Job.Status == RunningStatus);

        public async Task OnGetAsync(string? job, string? run, ulong? battleSeed)
        {
            var loaded = false;

            if (string.IsNullOrEmpty(job) == false)
                loaded = await LoadJobAsync(job);
            else if (string.IsNullOrEmpty(run) == false)
                loaded = await LoadRunAsync(run);

            if (loaded && battleSeed.HasValue)
                Seed = battleSeed.Value;

            await LoadReferenceAsync();

            if (CharacterId == 0 && 0 < Catalog.Items.Characters.Count)
                CharacterId = Catalog.Items.Characters[0].Id;

            if (Fight.Length == 0)
                Fight = FirstFight();

            if (LevelId == 0 && 0 < Catalog.StoryLevels.Count)
                LevelId = Catalog.StoryLevels[0].Id;

            FillSlots();

            if (loaded && battleSeed.HasValue)
            {
                var battle = await ExecuteAsync<BalanceBattleResultModel>(HttpMethod.Post, "/admin/balance/battle", CreateRequest(), "balance.battle", DescribeSetup() + " seed " + Seed, "Бой с сидом " + Seed + " посчитан.");

                Battle = battle.Data;
            }

            if (JobPending == false)
                await LoadListsAsync();
        }

        public string RunLink(string runId)
        {
            return "/Balance?env=" + Escape(CurrentEnvironment) + "&run=" + Escape(runId);
        }

        public string BattleLink(string runId, ulong seed)
        {
            return RunLink(runId) + "&battleSeed=" + seed.ToString(CultureInfo.InvariantCulture);
        }

        public string JobPercent()
        {
            if (Job == null || Job.Total <= 0)
                return "0";

            return Math.Min(100, Job.Done * 100 / Job.Total).ToString(CultureInfo.InvariantCulture);
        }

        public async Task<IActionResult> OnPostCharacteristicsAsync()
        {
            await LoadReferenceAsync();

            ParseEdits(EditsText, string.Empty);

            if (ReportEditErrors())
                return await PageWithListsAsync();

            var result = await ExecuteAsync<List<BalanceLayerModel>>(HttpMethod.Post, "/admin/balance/characteristics", CreateRequest(), "balance.characteristics", DescribeSetup(), "Характеристики посчитаны.");

            if (result.Data != null)
                Layers = result.Data;

            FillSlots();

            return await PageWithListsAsync();
        }

        public async Task<IActionResult> OnPostBattleAsync()
        {
            await LoadReferenceAsync();

            ParseEdits(EditsText, string.Empty);

            if (ReportEditErrors())
                return await PageWithListsAsync();

            var result = await ExecuteAsync<BalanceBattleResultModel>(HttpMethod.Post, "/admin/balance/battle", CreateRequest(), "balance.battle", DescribeSetup() + " seed " + Seed, "Бой посчитан.");

            Battle = result.Data;

            FillSlots();

            return await PageWithListsAsync();
        }

        public async Task<IActionResult> OnPostSeriesAsync()
        {
            await LoadReferenceAsync();

            ParseEdits(EditsText, string.Empty);

            if (ReportEditErrors())
                return await PageWithListsAsync();

            return await SubmitJobAsync("series", CreateRequest(), "balance.series", DescribeSetup() + " x" + Battles);
        }

        public async Task<IActionResult> OnPostLevelAsync()
        {
            await LoadReferenceAsync();

            ParseEdits(EditsText, string.Empty);

            if (ReportEditErrors())
                return await PageWithListsAsync();

            var request = new
            {
                version = Version,
                edits = ParseEdits(EditsText, string.Empty),
                setup = CreateSetup(),
                storyLevelId = LevelId,
                seed = (long)Seed,
                runs = LevelRuns,
                perkPolicy = PerkPolicy,
                perkPriority = ParseIds(PerkPriorityText),
                forkPolicy = ForkPolicy,
                scenario = SerializeState(),
            };
            return await SubmitJobAsync("level", request, "balance.level", DescribeSetup() + " level " + LevelId + " x" + LevelRuns + " " + PerkPolicy + "/" + ForkPolicy);
        }

        public async Task<IActionResult> OnPostCompareAsync()
        {
            await LoadReferenceAsync();

            var editsA = ParseEdits(EditsText, "A");
            var editsB = ParseEdits(EditsBText, "B");

            if (ReportEditErrors())
                return await PageWithListsAsync();

            var setup = CreateSetup();
            var request = new
            {
                kind = CompareKind,
                a = new { version = Version, edits = editsA, setup },
                b = new { version = VersionB, edits = editsB, setup },
                target = CreateTarget(),
                storyLevelId = LevelId,
                seed = (long)Seed,
                battles = Battles,
                runs = LevelRuns,
                perkPolicy = PerkPolicy,
                perkPriority = ParseIds(PerkPriorityText),
                forkPolicy = ForkPolicy,
                scenario = SerializeState(),
            };
            return await SubmitJobAsync("compare", request, "balance.compare", DescribeSetup() + " " + CompareKind + " A/B");
        }

        public async Task<IActionResult> OnPostSweepAsync()
        {
            await LoadReferenceAsync();

            var edits = ParseEdits(EditsText, string.Empty);

            if (ReportEditErrors())
                return await PageWithListsAsync();

            var request = new
            {
                kind = SweepKind,
                version = Version,
                edits,
                setup = CreateSetup(),
                target = CreateTarget(),
                storyLevelId = LevelId,
                seed = (long)Seed,
                battles = Battles,
                runs = LevelRuns,
                perkPolicy = PerkPolicy,
                perkPriority = ParseIds(PerkPriorityText),
                forkPolicy = ForkPolicy,
                axis = new { sheet = SweepSheet, row = SweepRow, column = SweepColumn, from = ParseDouble(SweepFrom), to = ParseDouble(SweepTo), step = ParseDouble(SweepStep) },
                scenario = SerializeState(),
            };
            return await SubmitJobAsync("sweep", request, "balance.sweep", DescribeSetup() + " " + SweepSheet + "/" + SweepRow + "/" + SweepColumn);
        }

        public async Task<IActionResult> OnPostSaveScenarioAsync()
        {
            await LoadReferenceAsync();

            var name = ScenarioName.Trim();

            if (name.Length == 0)
            {
                ErrorMessage = "Укажите название сценария.";
                StatusMessage = null;
                FillSlots();

                return await PageWithListsAsync();
            }

            var result = await ExecuteAsync<BalanceScenarioModel>(HttpMethod.Post, "/admin/balance/scenarios", new { name, payload = SerializeState() }, "balance.scenario.save", name, "Сценарий «" + name + "» сохранён.");

            if (result.Data != null)
                ScenarioId = result.Data.Id;

            FillSlots();

            return await PageWithListsAsync();
        }

        public async Task<IActionResult> OnPostLoadScenarioAsync()
        {
            var scenarioId = ScenarioId ?? string.Empty;
            var scenario = scenarioId.Length == 0 ? null : await LoadAsync<BalanceScenarioModel>("/admin/balance/scenarios/" + Escape(scenarioId));
            var applied = scenario != null && ApplyState(scenario.Payload);

            await LoadReferenceAsync();

            if (applied == false)
            {
                ErrorMessage = "Сценарий не найден или не читается.";
                StatusMessage = null;
                FillSlots();

                return await PageWithListsAsync();
            }

            ScenarioId = scenarioId;
            ScenarioName = scenario!.Name;
            StatusMessage = "Сценарий «" + scenario.Name + "» загружен.";
            ErrorMessage = null;
            FillSlots();

            return await PageWithListsAsync();
        }

        public async Task<IActionResult> OnPostDeleteScenarioAsync()
        {
            var denied = RequireAdmin();

            if (denied != null)
                return denied;

            await LoadReferenceAsync();

            if (ScenarioId.Length != 0)
                await ExecuteAsync<object>(HttpMethod.Delete, "/admin/balance/scenarios/" + Escape(ScenarioId), null, "balance.scenario.delete", ScenarioId, "Сценарий удалён.");

            ScenarioId = string.Empty;
            FillSlots();

            return await PageWithListsAsync();
        }

        public async Task<IActionResult> OnPostOpenHistoryAsync()
        {
            var historyId = HistoryId ?? string.Empty;
            var run = historyId.Length == 0 ? null : await LoadAsync<BalanceRunModel>("/admin/balance/history/" + Escape(historyId));
            var applied = run != null && ApplyState(run.Payload);

            await LoadReferenceAsync();

            if (applied == false)
            {
                ErrorMessage = "Прогон из истории не найден или в нём нет сетапа.";
                StatusMessage = null;
                FillSlots();

                return await PageWithListsAsync();
            }

            StatusMessage = "Сетап прогона " + run!.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " (" + run.Summary + ") загружен в форму.";
            ErrorMessage = null;
            FillSlots();

            return await PageWithListsAsync();
        }

        public string ChartPoints(Func<BalanceSweepPointModel, float> read, float scale, int width, int height)
        {
            if (Sweep == null || Sweep.Points.Count == 0)
                return string.Empty;

            var points = new List<string>(Math.Min(Sweep.Points.Count, MaxChartPoints));
            var count = Sweep.Points.Count;

            for (int i = 0; i < count && i < MaxChartPoints; i++)
            {
                var point = Sweep.Points[i];

                if (point.Error.Length != 0)
                    continue;

                var x = count == 1 ? width / 2f : width * i / (float)(count - 1);
                var value = scale <= 0f ? 0f : Math.Clamp(read(point) / scale, 0f, 1f);
                var y = height - value * height;

                points.Add(x.ToString("0.#", CultureInfo.InvariantCulture) + "," + y.ToString("0.#", CultureInfo.InvariantCulture));
            }

            return string.Join(" ", points);
        }

        public string ChartLabels(int width, int height)
        {
            if (Sweep == null || Sweep.Points.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            var count = Sweep.Points.Count;

            AppendLabel(builder, -6, 4, "end", "100 %");
            AppendLabel(builder, -6, height / 2f + 4, "end", "50 %");
            AppendLabel(builder, -6, height + 4, "end", "0 %");

            for (int i = 0; i < count && i < MaxChartPoints; i++)
            {
                if (12 < count && i % 2 == 1 && i != count - 1)
                    continue;

                var x = count == 1 ? width / 2f : width * i / (float)(count - 1);

                AppendLabel(builder, x, height + 20, "middle", Sweep.Points[i].Value);
            }

            return builder.ToString();
        }

        public async Task<IActionResult> OnPostProgressAsync()
        {
            await LoadReferenceAsync();

            var levelId = ProgressLevelId == 0 ? LevelId : ProgressLevelId;
            var progress = await LoadAsync<BalanceProgressModel>("/admin/balance/progress?storyLevelId=" + levelId.ToString(CultureInfo.InvariantCulture) + (Version.Length == 0 ? string.Empty : "&version=" + Escape(Version)));

            if (progress == null || progress.Players == 0)
            {
                ErrorMessage = "На окружении " + CurrentEnvironment + " нет игроков, у которых последний пройденный уровень — " + levelId + " (тестовые аккаунты не считаются).";
                StatusMessage = null;
                FillSlots();

                return await PageWithListsAsync();
            }

            ApplySetup(progress.Setup);
            Progress = progress;
            ProgressLevelId = levelId;
            StatusMessage = "Сетап типичного игрока после уровня " + levelId + " подставлен: " + progress.UserId + " из " + progress.Players + ".";
            ErrorMessage = null;
            FillSlots();

            return await PageWithListsAsync();
        }

        public async Task<IActionResult> OnPostLoadPlayerAsync()
        {
            await LoadReferenceAsync();

            var userId = LoadUserId.Trim();
            var profile = userId.Length == 0 ? null : await LoadAsync<PlayerProfileModel>("/admin/player/" + Escape(userId));

            if (profile == null)
            {
                ErrorMessage = "Профиль игрока " + userId + " не найден на окружении " + CurrentEnvironment + ".";
                StatusMessage = null;
                FillSlots();

                return await PageWithListsAsync();
            }

            ApplyProfile(profile);

            var run = await GameAdminClient.GetAsync<QaRunModel>(CurrentEnvironment, "/admin/qa/players/" + Escape(userId) + "/run", LoginName, HttpContext.RequestAborted);

            if (run.IsSuccess && run.Data != null)
                ApplyRun(run.Data);

            StatusMessage = "Сетап взят у игрока " + userId + (run.IsSuccess && run.Data != null ? " вместе с забегом." : ", забега нет.");
            FillSlots();

            return await PageWithListsAsync();
        }

        public string DescribeItem(List<CheatCatalogItemModel> items, int id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                    return items[i].Details.Length == 0 ? id + " · " + items[i].Name : id + " · " + items[i].Name + " · " + items[i].Details;
            }

            return id.ToString(CultureInfo.InvariantCulture);
        }

        public string FightValue(int levelId, int stageId, int eventId)
        {
            return levelId.ToString(CultureInfo.InvariantCulture) + TargetSeparator + stageId.ToString(CultureInfo.InvariantCulture) + TargetSeparator + eventId.ToString(CultureInfo.InvariantCulture);
        }

        public string Percent(float value)
        {
            return (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }

        public string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private async Task LoadReferenceAsync()
        {
            NormalizeInput();

            var versions = await LoadCachedAsync<List<BalanceVersionModel>>("/admin/balance/versions", TimeSpan.FromSeconds(VersionsLifetimeSeconds));

            if (versions != null)
                Versions = versions;

            var catalog = await LoadCachedAsync<BalanceCatalogModel>("/admin/balance/catalog" + (Version.Length == 0 ? string.Empty : "?version=" + Escape(Version)), Version.Length == 0 ? TimeSpan.FromSeconds(VersionsLifetimeSeconds) : TimeSpan.FromMinutes(PinnedCatalogLifetimeMinutes));

            if (catalog != null)
                Catalog = catalog;

        }

        private async Task<IActionResult> SubmitJobAsync(string kind, object request, string action, string target)
        {
            var result = await ExecuteAsync<BalanceJobCreatedModel>(HttpMethod.Post, "/admin/balance/jobs", new { kind, request }, action, target, "Прогон поставлен в очередь.");

            if (result.Data == null || result.Data.Id.Length == 0)
            {
                FillSlots();

                return await PageWithListsAsync();
            }

            return Redirect("/Balance?env=" + Escape(CurrentEnvironment) + "&job=" + Escape(result.Data.Id));
        }

        private async Task<bool> LoadJobAsync(string jobId)
        {
            Job = await LoadAsync<BalanceJobModel>("/admin/balance/jobs/" + Escape(jobId));

            if (Job == null)
                return false;

            var applied = ApplyState(ReadScenario(Job.Request));

            if (Job.Status == FailedStatus)
            {
                ErrorMessage = "Прогон не удался: " + Job.Error;
                StatusMessage = null;
            }

            if (Job.Status != DoneStatus || Job.Result.HasValue == false)
                return applied;

            var result = Job.Result.Value;

            if (Job.Kind == "series")
                Series = result.Deserialize<BalanceSeriesResultModel>(_jsonOptions);
            else if (Job.Kind == "level")
                Level = result.Deserialize<BalanceLevelResultModel>(_jsonOptions);
            else if (Job.Kind == "compare")
                Compare = result.Deserialize<BalanceCompareResultModel>(_jsonOptions);
            else if (Job.Kind == "sweep")
                Sweep = result.Deserialize<BalanceSweepResultModel>(_jsonOptions);

            return applied;
        }

        private async Task<bool> LoadRunAsync(string runId)
        {
            var run = await LoadAsync<BalanceRunModel>("/admin/balance/history/" + Escape(runId));

            if (run == null || ApplyState(run.Payload) == false)
            {
                ErrorMessage = "Прогон " + runId + " не найден в истории или в нём нет сетапа.";
                StatusMessage = null;

                return false;
            }

            return true;
        }

        private string ReadScenario(string request)
        {
            if (string.IsNullOrWhiteSpace(request))
                return string.Empty;

            try
            {
                using var json = JsonDocument.Parse(request);

                if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty(ScenarioField, out var scenario) && scenario.ValueKind == JsonValueKind.String)
                    return scenario.GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
            }

            return string.Empty;
        }

        private void ApplySetup(BalanceSetupModel setup)
        {
            CharacterId = setup.CharacterId;
            PromoteLevel = setup.PromoteLevel;
            Summons = new List<BalanceSummonModel>(setup.Summons);
            Equipment = new List<BalanceEquipmentModel>(setup.Equipment);

            var bonuses = new List<string>(setup.AccountBonuses.Count);

            for (int i = 0; i < setup.AccountBonuses.Count; i++)
                bonuses.Add(setup.AccountBonuses[i].Id.ToString(CultureInfo.InvariantCulture) + ":" + setup.AccountBonuses[i].Count.ToString(CultureInfo.InvariantCulture));

            AccountBonusesText = string.Join(", ", bonuses);
        }

        private async Task<T?> LoadCachedAsync<T>(string path, TimeSpan lifetime)
            where T : class
        {
            var key = CacheKeyPrefix + CurrentEnvironment + path;

            if (_memoryCache.TryGetValue(key, out T? cached) && cached != null)
                return cached;

            var value = await LoadAsync<T>(path);

            if (value != null)
                _memoryCache.Set(key, value, lifetime);

            return value;
        }

        private async Task<IActionResult> PageWithListsAsync()
        {
            await LoadListsAsync();

            return Page();
        }

        private async Task LoadListsAsync()
        {
            var scenarios = await LoadAsync<List<BalanceScenarioModel>>("/admin/balance/scenarios");

            if (scenarios != null)
                Scenarios = scenarios;

            var history = await LoadAsync<List<BalanceRunModel>>("/admin/balance/history?limit=" + HistoryLimit.ToString(CultureInfo.InvariantCulture));

            if (history != null)
                History = history;
        }

        private List<BalanceConfigEditModel> ParseEdits(string text, string label)
        {
            var edits = new List<BalanceConfigEditModel>();
            var lines = (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (line.Length == 0)
                    continue;

                var parts = line.Split(EditSeparator);

                if (parts.Length != 4)
                {
                    EditErrors.Add((label.Length == 0 ? string.Empty : label + ", ") + "строка " + (i + 1) + ": нужно «лист | строка | колонка | значение», а не «" + line + "»");

                    continue;
                }

                edits.Add(new BalanceConfigEditModel { Sheet = parts[0].Trim(), Row = parts[1].Trim(), Column = parts[2].Trim(), Value = parts[3].Trim() });
            }

            return edits;
        }

        private bool ReportEditErrors()
        {
            if (EditErrors.Count == 0)
                return false;

            ErrorMessage = "Черновые правки не разобраны: " + string.Join("; ", EditErrors);
            StatusMessage = null;
            FillSlots();

            return true;
        }

        private double ParseDouble(string text)
        {
            return double.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private string SerializeState()
        {
            return JsonSerializer.Serialize(new BalanceFormState
            {
                Version = Version,
                CharacterId = CharacterId,
                PromoteLevel = PromoteLevel,
                CurrentHealth = CurrentHealth,
                Summons = Summons,
                Equipment = Equipment,
                AccountBonusesText = AccountBonusesText,
                RunPerksText = RunPerksText,
                RunBonusesText = RunBonusesText,
                RunStatusesText = RunStatusesText,
                EditsText = EditsText,
                Fight = Fight,
                EnemiesText = EnemiesText,
                Seed = Seed,
                Battles = Battles,
                LevelId = LevelId,
                LevelRuns = LevelRuns,
                PerkPolicy = PerkPolicy,
                PerkPriorityText = PerkPriorityText,
                ForkPolicy = ForkPolicy,
                VersionB = VersionB,
                EditsBText = EditsBText,
                CompareKind = CompareKind,
                SweepKind = SweepKind,
                SweepSheet = SweepSheet,
                SweepRow = SweepRow,
                SweepColumn = SweepColumn,
                SweepFrom = SweepFrom,
                SweepTo = SweepTo,
                SweepStep = SweepStep,
            });
        }

        private bool ApplyState(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return false;

            BalanceFormState? state;

            try
            {
                state = JsonSerializer.Deserialize<BalanceFormState>(payload);
            }
            catch (JsonException)
            {
                return false;
            }

            if (state == null)
                return false;

            Version = state.Version;
            CharacterId = state.CharacterId;
            PromoteLevel = state.PromoteLevel;
            CurrentHealth = state.CurrentHealth;
            Summons = state.Summons;
            Equipment = state.Equipment;
            AccountBonusesText = state.AccountBonusesText;
            RunPerksText = state.RunPerksText;
            RunBonusesText = state.RunBonusesText;
            RunStatusesText = state.RunStatusesText;
            EditsText = state.EditsText;
            Fight = state.Fight;
            EnemiesText = state.EnemiesText;
            Seed = state.Seed;
            Battles = state.Battles;
            LevelId = state.LevelId;
            LevelRuns = state.LevelRuns;
            PerkPolicy = state.PerkPolicy;
            PerkPriorityText = state.PerkPriorityText;
            ForkPolicy = state.ForkPolicy;
            VersionB = state.VersionB;
            EditsBText = state.EditsBText;
            CompareKind = state.CompareKind;
            SweepKind = state.SweepKind;
            SweepSheet = state.SweepSheet;
            SweepRow = state.SweepRow;
            SweepColumn = state.SweepColumn;
            SweepFrom = state.SweepFrom;
            SweepTo = state.SweepTo;
            SweepStep = state.SweepStep;
            NormalizeInput();

            return true;
        }

        private void AppendLabel(StringBuilder builder, float x, float y, string anchor, string text)
        {
            builder.Append("<text x=\"").Append(x.ToString("0.#", CultureInfo.InvariantCulture))
                .Append("\" y=\"").Append(y.ToString("0.#", CultureInfo.InvariantCulture))
                .Append("\" font-size=\"11\" text-anchor=\"").Append(anchor)
                .Append("\" fill=\"currentColor\">").Append(WebUtility.HtmlEncode(text)).Append("</text>");
        }

        private void NormalizeInput()
        {
            Version = Version ?? string.Empty;
            AccountBonusesText = AccountBonusesText ?? string.Empty;
            RunPerksText = RunPerksText ?? string.Empty;
            RunBonusesText = RunBonusesText ?? string.Empty;
            RunStatusesText = RunStatusesText ?? string.Empty;
            Fight = Fight ?? string.Empty;
            EnemiesText = EnemiesText ?? string.Empty;
            LoadUserId = LoadUserId ?? string.Empty;
            PerkPolicy = PerkPolicy ?? "first";
            PerkPriorityText = PerkPriorityText ?? string.Empty;
            ForkPolicy = ForkPolicy ?? "firstButton";
            EditsText = EditsText ?? string.Empty;
            VersionB = VersionB ?? string.Empty;
            EditsBText = EditsBText ?? string.Empty;
            CompareKind = CompareKind ?? "series";
            SweepKind = SweepKind ?? "series";
            SweepSheet = SweepSheet ?? string.Empty;
            SweepRow = SweepRow ?? string.Empty;
            SweepColumn = SweepColumn ?? string.Empty;
            SweepFrom = SweepFrom ?? string.Empty;
            SweepTo = SweepTo ?? string.Empty;
            SweepStep = SweepStep ?? string.Empty;
            ScenarioName = ScenarioName ?? string.Empty;
            ScenarioId = ScenarioId ?? string.Empty;
            HistoryId = HistoryId ?? string.Empty;
            Summons = Summons ?? new List<BalanceSummonModel>();
            Equipment = Equipment ?? new List<BalanceEquipmentModel>();
        }

        private object CreateRequest()
        {
            return new
            {
                version = Version,
                edits = ParseEdits(EditsText, string.Empty),
                setup = CreateSetup(),
                target = CreateTarget(),
                seed = Seed,
                battles = Battles,
                includeLog = true,
                scenario = SerializeState(),
            };
        }

        private BalanceSetupModel CreateSetup()
        {
            var setup = new BalanceSetupModel
            {
                CharacterId = CharacterId,
                PromoteLevel = PromoteLevel,
                CurrentHealth = CurrentHealth,
                AccountBonuses = ParseBonuses(AccountBonusesText),
                RunPerks = ParseIds(RunPerksText),
                RunBonuses = ParseBonuses(RunBonusesText),
                RunStatuses = ParseIds(RunStatusesText),
            };

            for (int i = 0; i < Summons.Count; i++)
            {
                if (0 < Summons[i].Id)
                    setup.Summons.Add(Summons[i]);
            }

            for (int i = 0; i < Equipment.Count; i++)
            {
                if (0 < Equipment[i].ConfigId)
                    setup.Equipment.Add(Equipment[i]);
            }

            return setup;
        }

        private BalanceTargetModel CreateTarget()
        {
            var target = new BalanceTargetModel { EnemyIds = ParseIds(EnemiesText) };
            var parts = Fight.Split(TargetSeparator);

            if (parts.Length == 3)
            {
                target.StoryLevelId = ParseInt(parts[0]);
                target.StageId = ParseInt(parts[1]);
                target.EventId = ParseInt(parts[2]);
            }

            return target;
        }

        private void ApplyProfile(PlayerProfileModel profile)
        {
            CharacterId = profile.Loadout.CharacterId;
            PromoteLevel = 1;
            Summons = new List<BalanceSummonModel>();
            Equipment = new List<BalanceEquipmentModel>();

            for (int i = 0; i < profile.Characters.Count; i++)
            {
                if (profile.Characters[i].Id == CharacterId)
                    PromoteLevel = profile.Characters[i].PromoteLevel;
            }

            for (int i = 0; i < profile.Loadout.Summons.Count; i++)
            {
                for (int summonIndex = 0; summonIndex < profile.Summons.Count; summonIndex++)
                {
                    var summon = profile.Summons[summonIndex];

                    if (summon.Id == profile.Loadout.Summons[i])
                        Summons.Add(new BalanceSummonModel { Id = summon.Id, Level = summon.Level, MasteryLevel = summon.MasteryLevel, SkillLevels = new List<int>(summon.SkillLevels) });
                }
            }

            foreach (var pair in profile.Loadout.Equipment)
            {
                for (int i = 0; i < profile.Equipment.Count; i++)
                {
                    var item = profile.Equipment[i];

                    if (string.Equals(item.InstanceId, pair.Value, StringComparison.Ordinal))
                        Equipment.Add(new BalanceEquipmentModel { ConfigId = item.ConfigId, Level = item.Level });
                }
            }

            var bonuses = new List<string>(profile.Bonuses.Count);

            for (int i = 0; i < profile.Bonuses.Count; i++)
                bonuses.Add(profile.Bonuses[i].Id.ToString(CultureInfo.InvariantCulture) + ":" + profile.Bonuses[i].Count.ToString(CultureInfo.InvariantCulture));

            AccountBonusesText = string.Join(", ", bonuses);
        }

        private void ApplyRun(QaRunModel run)
        {
            CurrentHealth = run.CurrentHealth;
            RunPerksText = string.Join(", ", run.Perks);
            RunStatusesText = string.Join(", ", run.Statuses);

            var bonuses = new List<string>(run.Bonuses.Count);

            for (int i = 0; i < run.Bonuses.Count; i++)
                bonuses.Add(run.Bonuses[i].BonusId + ":" + run.Bonuses[i].Count + ":" + run.Bonuses[i].RemainingBattles);

            RunBonusesText = string.Join(", ", bonuses);
        }

        private void FillSlots()
        {
            while (Summons.Count < SummonSlots)
                Summons.Add(new BalanceSummonModel());

            while (Equipment.Count < EquipmentSlots)
                Equipment.Add(new BalanceEquipmentModel());
        }

        private string FirstFight()
        {
            for (int levelIndex = 0; levelIndex < Catalog.StoryLevels.Count; levelIndex++)
            {
                var level = Catalog.StoryLevels[levelIndex];

                for (int stageIndex = 0; stageIndex < level.Stages.Count; stageIndex++)
                {
                    var stage = level.Stages[stageIndex];

                    for (int eventIndex = 0; eventIndex < stage.Events.Count; eventIndex++)
                    {
                        if (stage.Events[eventIndex].EnemyIds.Count != 0)
                            return FightValue(level.Id, stage.Id, stage.Events[eventIndex].Id);
                    }
                }
            }

            return string.Empty;
        }

        private string DescribeSetup()
        {
            return $"character {CharacterId} promote {PromoteLevel} fight {Fight} enemies {EnemiesText} version {(Version.Length == 0 ? "master" : Version)}";
        }

        private List<BalanceBonusModel> ParseBonuses(string text)
        {
            var bonuses = new List<BalanceBonusModel>();
            var entries = (text ?? string.Empty).Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < entries.Length; i++)
            {
                var parts = entries[i].Trim().Split(':');
                var id = ParseInt(parts[0]);

                if (id <= 0)
                    continue;

                bonuses.Add(new BalanceBonusModel
                {
                    Id = id,
                    Count = 1 < parts.Length ? Math.Max(1, ParseInt(parts[1])) : 1,
                    RemainingBattles = 2 < parts.Length ? Math.Max(0, ParseInt(parts[2])) : 0,
                });
            }

            return bonuses;
        }

        private List<int> ParseIds(string text)
        {
            var ids = new List<int>();
            var entries = (text ?? string.Empty).Split(new[] { ',', ';', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < entries.Length; i++)
            {
                var id = ParseInt(entries[i]);

                if (0 < id)
                    ids.Add(id);
            }

            return ids;
        }

        private int ParseInt(string text)
        {
            return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
    }
}

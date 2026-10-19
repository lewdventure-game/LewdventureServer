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
        public const string BattleTab = "battle";
        public const string LevelTab = "level";
        public const string CompareTab = "compare";
        public const string SweepTab = "sweep";
        public const string HistoryTab = "history";
        public const string SeriesKind = "series";
        public const string LevelKind = "level";
        public const string CompareKind = "compare";
        public const string SweepKind = "sweep";

        private const char TargetSeparator = '|';
        private const char LegacyEditSeparator = '|';
        private const int HistoryLimit = 30;
        private const int MaxChartPoints = 100;
        private const int VersionsLifetimeSeconds = 30;
        private const int PinnedCatalogLifetimeMinutes = 30;
        private const string CacheKeyPrefix = "balance:";
        private const string QueuedStatus = "queued";
        private const string RunningStatus = "running";
        private const string DoneStatus = "done";
        private const string FailedStatus = "failed";
        private const string ScenarioField = "scenario";
        private const string AdminActorPrefix = "admin:";
        private const float SureRate = 0.9f;
        private const float RiskyRate = 0.6f;
        private const float HardRate = 0.3f;

        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IMemoryCache _memoryCache;

        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient, IMemoryCache memoryCache)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
            _memoryCache = memoryCache;
        }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = BattleTab;

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
        public List<BalanceConfigEditModel> Edits { get; set; } = new();

        [BindProperty]
        public string Fight { get; set; } = string.Empty;

        [BindProperty]
        public string EnemiesText { get; set; } = string.Empty;

        [BindProperty]
        public ulong Seed { get; set; } = 1;

        [BindProperty]
        public int Battles { get; set; } = 500;

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
        public string VersionB { get; set; } = string.Empty;

        [BindProperty]
        public List<BalanceConfigEditModel> EditsB { get; set; } = new();

        [BindProperty]
        public string CompareWhat { get; set; } = SeriesKind;

        [BindProperty]
        public string SweepWhat { get; set; } = SeriesKind;

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
        public string LoadUserId { get; set; } = string.Empty;

        [BindProperty]
        public int ProgressLevelId { get; set; }

        public IReadOnlyList<int> BattlePresets { get; } = new[] { 1, 100, 500, 2000 };

        public IReadOnlyList<int> LevelRunPresets { get; } = new[] { 50, 100, 300 };

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

        public bool HasResult => Layers.Count != 0 || Battle != null || Series != null || Level != null || Compare != null || Sweep != null || Job != null;

        public async Task OnGetAsync(string? job, string? run, ulong? battleSeed)
        {
            var loaded = false;

            if (string.IsNullOrEmpty(job) == false)
                loaded = await LoadJobAsync(job);
            else if (string.IsNullOrEmpty(run) == false)
                loaded = await LoadRunAsync(run);

            if (loaded && battleSeed.HasValue)
            {
                Seed = battleSeed.Value;
                Tab = BattleTab;
            }

            await LoadReferenceAsync();
            ApplyDefaults();

            if (loaded && battleSeed.HasValue && CollectAllEdits())
            {
                var battle = await ExecuteAsync<BalanceBattleResultModel>(HttpMethod.Post, "/admin/balance/battle", CreateFightRequest(), "balance.battle", DescribeSetup() + " seed " + Seed, "Бой с сидом " + Seed + " посчитан пошагово.");

                Battle = battle.Data;
            }

            if (JobPending == false)
                await LoadListsAsync();
        }

        public async Task<IActionResult> OnPostCharacteristicsAsync()
        {
            await LoadReferenceAsync();

            if (CollectAllEdits() == false)
                return await ShowAsync();

            var result = await ExecuteAsync<List<BalanceLayerModel>>(HttpMethod.Post, "/admin/balance/characteristics", CreateFightRequest(), "balance.characteristics", DescribeSetup(), "Характеристики героя посчитаны.");

            if (result.Data != null)
                Layers = result.Data;

            return await ShowAsync();
        }

        public async Task<IActionResult> OnPostFightAsync()
        {
            Tab = BattleTab;
            await LoadReferenceAsync();

            if (CollectAllEdits() == false)
                return await ShowAsync();

            if (Battles <= 1)
            {
                var result = await ExecuteAsync<BalanceBattleResultModel>(HttpMethod.Post, "/admin/balance/battle", CreateFightRequest(), "balance.battle", DescribeSetup() + " seed " + Seed, "Бой посчитан пошагово.");

                Battle = result.Data;

                return await ShowAsync();
            }

            return await SubmitJobAsync(SeriesKind, CreateFightRequest(), "balance.series", DescribeSetup() + " x" + Battles);
        }

        public async Task<IActionResult> OnPostLevelAsync()
        {
            Tab = LevelTab;
            await LoadReferenceAsync();

            if (CollectAllEdits() == false)
                return await ShowAsync();

            return await SubmitJobAsync(LevelKind, CreateLevelRequest(), "balance.level", DescribeSetup() + " level " + LevelId + " x" + LevelRuns + " " + PerkPolicy + "/" + ForkPolicy);
        }

        public async Task<IActionResult> OnPostCompareAsync()
        {
            Tab = CompareTab;
            await LoadReferenceAsync();

            if (CollectAllEdits() == false)
                return await ShowAsync();

            var setup = CreateSetup();
            var request = new
            {
                kind = CompareWhat,
                a = new { version = Version, edits = CleanEdits(Edits), setup },
                b = new { version = VersionB, edits = CleanEdits(EditsB), setup },
                target = CreateTarget(),
                storyLevelId = LevelId,
                seed = (long)Seed,
                battles = Math.Max(1, Battles),
                runs = LevelRuns,
                perkPolicy = PerkPolicy,
                perkPriority = ParseIds(PerkPriorityText),
                forkPolicy = ForkPolicy,
                scenario = SerializeState(),
            };

            return await SubmitJobAsync(CompareKind, request, "balance.compare", DescribeSetup() + " " + CompareWhat + " A/B");
        }

        public async Task<IActionResult> OnPostSweepAsync()
        {
            Tab = SweepTab;
            await LoadReferenceAsync();

            if (CollectAllEdits() == false)
                return await ShowAsync();

            var request = new
            {
                kind = SweepWhat,
                version = Version,
                edits = CleanEdits(Edits),
                setup = CreateSetup(),
                target = CreateTarget(),
                storyLevelId = LevelId,
                seed = (long)Seed,
                battles = Math.Max(1, Battles),
                runs = LevelRuns,
                perkPolicy = PerkPolicy,
                perkPriority = ParseIds(PerkPriorityText),
                forkPolicy = ForkPolicy,
                axis = new { sheet = SweepSheet, row = SweepRow, column = SweepColumn, from = ParseDouble(SweepFrom), to = ParseDouble(SweepTo), step = ParseDouble(SweepStep) },
                scenario = SerializeState(),
            };

            return await SubmitJobAsync(SweepKind, request, "balance.sweep", DescribeSetup() + " " + SweepSheet + "/" + SweepRow + "/" + SweepColumn);
        }

        public async Task<IActionResult> OnPostSaveScenarioAsync()
        {
            await LoadReferenceAsync();

            var name = ScenarioName.Trim();

            if (name.Length == 0)
            {
                ShowError("Укажите название сценария.");

                return await ShowAsync();
            }

            var result = await ExecuteAsync<BalanceScenarioModel>(HttpMethod.Post, "/admin/balance/scenarios", new { name, payload = SerializeState() }, "balance.scenario.save", name, "Сценарий «" + name + "» сохранён.");

            if (result.Data != null)
                ScenarioId = result.Data.Id;

            return await ShowAsync();
        }

        public async Task<IActionResult> OnPostLoadScenarioAsync()
        {
            var scenarioId = ScenarioId ?? string.Empty;
            var scenario = scenarioId.Length == 0 ? null : await LoadAsync<BalanceScenarioModel>("/admin/balance/scenarios/" + Escape(scenarioId));
            var applied = scenario != null && ApplyState(scenario.Payload);

            await LoadReferenceAsync();

            if (applied == false)
            {
                ShowError(scenarioId.Length == 0 ? "Выберите сценарий в списке." : "Сценарий не найден или не читается.");

                return await ShowAsync();
            }

            ScenarioId = scenarioId;
            ScenarioName = scenario!.Name;
            ShowStatus("Сценарий «" + scenario.Name + "» загружен.");

            return await ShowAsync();
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

            return await ShowAsync();
        }

        public async Task<IActionResult> OnPostOpenHistoryAsync()
        {
            var historyId = HistoryId ?? string.Empty;
            var run = historyId.Length == 0 ? null : await LoadAsync<BalanceRunModel>("/admin/balance/history/" + Escape(historyId));
            var applied = run != null && ApplyState(run.Payload);

            await LoadReferenceAsync();

            if (applied == false)
            {
                ShowError("Прогон из истории не найден или в нём нет сетапа.");

                return await ShowAsync();
            }

            ShowStatus("Сетап прогона " + run!.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " (" + run.Summary + ") загружен — запустите его снова.");

            return await ShowAsync();
        }

        public async Task<IActionResult> OnPostLoadPlayerAsync()
        {
            await LoadReferenceAsync();

            var userId = LoadUserId.Trim();
            var profile = userId.Length == 0 ? null : await LoadAsync<PlayerProfileModel>("/admin/player/" + Escape(userId));

            if (profile == null)
            {
                ShowError(userId.Length == 0 ? "Укажите userId игрока." : "Профиль игрока " + userId + " не найден на окружении " + CurrentEnvironment + ".");

                return await ShowAsync();
            }

            ApplyProfile(profile);

            var run = await GameAdminClient.GetAsync<QaRunModel>(CurrentEnvironment, "/admin/qa/players/" + Escape(userId) + "/run", LoginName, HttpContext.RequestAborted);

            if (run.IsSuccess && run.Data != null)
                ApplyRun(run.Data);

            ShowStatus("Сетап взят у игрока " + userId + (run.IsSuccess && run.Data != null ? " вместе с его текущим забегом." : ", забега у него нет."));

            return await ShowAsync();
        }

        public async Task<IActionResult> OnPostProgressAsync()
        {
            await LoadReferenceAsync();

            var levelId = ProgressLevelId == 0 ? LevelId : ProgressLevelId;
            var progress = await LoadAsync<BalanceProgressModel>("/admin/balance/progress?storyLevelId=" + levelId.ToString(CultureInfo.InvariantCulture) + (Version.Length == 0 ? string.Empty : "&version=" + Escape(Version)));

            if (progress == null || progress.Players == 0)
            {
                ShowError("На окружении " + CurrentEnvironment + " нет игроков, которые прошли уровень " + levelId + " и не прошли следующий (тестовые аккаунты не считаются).");

                return await ShowAsync();
            }

            ApplySetup(progress.Setup);
            Progress = progress;
            ProgressLevelId = levelId;
            ShowStatus("Подставлен сетап типичного игрока, который прошёл уровень " + levelId + ": " + progress.UserId + ".");

            return await ShowAsync();
        }

        public bool IsTab(string tab)
        {
            return string.Equals(Tab, tab, StringComparison.Ordinal);
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

        public string JobTitle()
        {
            if (Job == null)
                return string.Empty;

            if (Job.Kind == SeriesKind)
                return "Серия боёв";

            if (Job.Kind == LevelKind)
                return "Прогон уровня";

            if (Job.Kind == CompareKind)
                return "Сравнение A и B";

            return "Перебор параметра";
        }

        public string CharacterName()
        {
            var items = Catalog.Items.Characters;

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == CharacterId)
                    return Label(items[i], "Персонаж");
            }

            return "Персонаж " + CharacterId;
        }

        public string ActorName(string actor)
        {
            return actor.StartsWith(AdminActorPrefix, StringComparison.Ordinal) ? actor.Substring(AdminActorPrefix.Length) : actor;
        }

        public string Label(CheatCatalogItemModel item, string kind)
        {
            return string.IsNullOrWhiteSpace(item.Name) ? kind + " " + item.Id : item.Name + " (id " + item.Id + ")";
        }

        public string SetupSummary()
        {
            var parts = new List<string> { CharacterName() + ", промоут " + PromoteLevel };
            var summons = CountFilled(Summons);
            var equipment = CountFilledEquipment(Equipment);
            var bonuses = ParseBonuses(AccountBonusesText);

            parts.Add(summons == 0 ? "без саммонов" : "саммонов " + summons);
            parts.Add(equipment == 0 ? "без снаряжения" : "предметов " + equipment);
            parts.Add(bonuses.Count == 0 ? "без бонусов аккаунта" : "бонусов аккаунта " + bonuses.Count);

            if (HasRunState())
                parts.Add("с состоянием забега");

            return string.Join(" · ", parts);
        }

        public string ConfigSummary()
        {
            var edits = CleanEdits(Edits).Count;
            var version = Version.Length == 0 ? "мастер" : ShortVersion(Version);

            return edits == 0 ? "Конфиги: " + version : "Конфиги: " + version + " + " + edits + " " + EditsWord(edits);
        }

        public string ShortVersion(string version)
        {
            for (int i = 0; i < Versions.Count; i++)
            {
                if (string.Equals(Versions[i].Version, version, StringComparison.Ordinal))
                    return Versions[i].ShortVersion;
            }

            return version.Length <= 20 ? version : version.Substring(0, 20) + "…";
        }

        public bool HasRunState()
        {
            return 0f < CurrentHealth || RunPerksText.Trim().Length != 0 || RunBonusesText.Trim().Length != 0 || RunStatusesText.Trim().Length != 0;
        }

        public int CountFilled(List<BalanceSummonModel> summons)
        {
            var count = 0;

            for (int i = 0; i < summons.Count; i++)
            {
                if (0 < summons[i].Id)
                    count += 1;
            }

            return count;
        }

        public int CountFilledEquipment(List<BalanceEquipmentModel> equipment)
        {
            var count = 0;

            for (int i = 0; i < equipment.Count; i++)
            {
                if (0 < equipment[i].ConfigId)
                    count += 1;
            }

            return count;
        }

        public BalanceVerdict Verdict(float rate, string subject)
        {
            if (SureRate <= rate)
                return new BalanceVerdict { Title = subject + " проходится уверенно", Tone = "good" };

            if (RiskyRate <= rate)
                return new BalanceVerdict { Title = subject + " проходится, но с риском", Tone = "fair" };

            if (HardRate <= rate)
                return new BalanceVerdict { Title = subject + " сложный", Tone = "hard" };

            return new BalanceVerdict { Title = subject + " почти не проходится", Tone = "bad" };
        }

        public BalanceLevelStageStatsModel? DeadliestStage(BalanceLevelResultModel level)
        {
            BalanceLevelStageStatsModel? deadliest = null;

            for (int i = 0; i < level.Stages.Count; i++)
            {
                var stage = level.Stages[i];

                if (0 < stage.Deaths && (deadliest == null || deadliest.Deaths < stage.Deaths))
                    deadliest = stage;
            }

            return deadliest;
        }

        public string SweepThreshold()
        {
            if (Sweep == null || Sweep.Points.Count < 2)
                return string.Empty;

            var isLevel = Sweep.Kind == LevelKind;

            for (int i = 1; i < Sweep.Points.Count; i++)
            {
                var previous = Sweep.Points[i - 1];
                var current = Sweep.Points[i];

                if (previous.Error.Length != 0 || current.Error.Length != 0)
                    continue;

                var before = isLevel ? previous.CompletionRate : previous.WinRate;
                var after = isLevel ? current.CompletionRate : current.WinRate;

                if ((before - 0.5f) * (after - 0.5f) <= 0f && Math.Abs(before - after) > 0.0001f)
                    return "Порог 50 % " + (isLevel ? "прохождения" : "побед") + " — между " + Sweep.Axis.Column + " = " + previous.Value + " (" + Percent(before) + ") и " + current.Value + " (" + Percent(after) + ").";
            }

            return "Во всём диапазоне " + (isLevel ? "доля прохождения" : "процент побед") + " не пересекает 50 %.";
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

        public BalanceEditsEditor EditsEditor()
        {
            return new BalanceEditsEditor { Prefix = nameof(Edits), Rows = EditRows(Edits), Sheets = Catalog.Sheets };
        }

        public BalanceEditsEditor EditsEditorB()
        {
            return new BalanceEditsEditor { Prefix = nameof(EditsB), Rows = EditRows(EditsB), Sheets = Catalog.Sheets };
        }

        public List<BalanceConfigEditModel> EditRows(List<BalanceConfigEditModel> edits)
        {
            var rows = CleanEdits(edits);

            rows.Add(new BalanceConfigEditModel());

            return rows;
        }

        public string Percent(float value)
        {
            return (value * 100f).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }

        public string Number(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        public string ChartPoints(Func<BalanceSweepPointModel, float> read, int width, int height)
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
                var y = height - Math.Clamp(read(point), 0f, 1f) * height;

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

        private async Task<IActionResult> ShowAsync()
        {
            ApplyDefaults();
            await LoadListsAsync();

            return Page();
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            StatusMessage = null;
        }

        private void ShowStatus(string message)
        {
            StatusMessage = message;
            ErrorMessage = null;
        }

        private void ApplyDefaults()
        {
            if (CharacterId == 0 && 0 < Catalog.Items.Characters.Count)
                CharacterId = Catalog.Items.Characters[0].Id;

            if (Fight.Length == 0)
                Fight = FirstFight();

            if (LevelId == 0 && 0 < Catalog.StoryLevels.Count)
                LevelId = Catalog.StoryLevels[0].Id;

            if (ProgressLevelId == 0)
                ProgressLevelId = LevelId;

            FillSlots();
        }

        private async Task LoadReferenceAsync()
        {
            NormalizeInput();

            var versions = await LoadCachedAsync<List<BalanceVersionModel>>("/admin/balance/versions", TimeSpan.FromSeconds(VersionsLifetimeSeconds));

            if (versions != null)
                Versions = versions;

            var catalogLifetime = Version.Length == 0 ? TimeSpan.FromSeconds(VersionsLifetimeSeconds) : TimeSpan.FromMinutes(PinnedCatalogLifetimeMinutes);
            var catalog = await LoadCachedAsync<BalanceCatalogModel>("/admin/balance/catalog" + (Version.Length == 0 ? string.Empty : "?version=" + Escape(Version)), catalogLifetime);

            if (catalog != null)
                Catalog = catalog;
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

        private async Task LoadListsAsync()
        {
            var scenarios = await LoadAsync<List<BalanceScenarioModel>>("/admin/balance/scenarios");

            if (scenarios != null)
                Scenarios = scenarios;

            var history = await LoadAsync<List<BalanceRunModel>>("/admin/balance/history?limit=" + HistoryLimit.ToString(CultureInfo.InvariantCulture));

            if (history != null)
                History = history;
        }

        private async Task<IActionResult> SubmitJobAsync(string kind, object request, string action, string target)
        {
            var result = await ExecuteAsync<BalanceJobCreatedModel>(HttpMethod.Post, "/admin/balance/jobs", new { kind, request }, action, target, "Прогон поставлен в очередь.");

            if (result.Data == null || result.Data.Id.Length == 0)
                return await ShowAsync();

            return Redirect("/Balance?env=" + Escape(CurrentEnvironment) + "&job=" + Escape(result.Data.Id));
        }

        private async Task<bool> LoadJobAsync(string jobId)
        {
            Job = await LoadAsync<BalanceJobModel>("/admin/balance/jobs/" + Escape(jobId));

            if (Job == null)
                return false;

            var applied = ApplyState(ReadScenario(Job.Request));

            Tab = Job.Kind == LevelKind ? LevelTab : Job.Kind == CompareKind ? CompareTab : Job.Kind == SweepKind ? SweepTab : BattleTab;

            if (Job.Status == FailedStatus)
                ShowError("Прогон не удался: " + Job.Error);

            if (Job.Status != DoneStatus || Job.Result.HasValue == false)
                return applied;

            var result = Job.Result.Value;

            if (Job.Kind == SeriesKind)
                Series = result.Deserialize<BalanceSeriesResultModel>(_jsonOptions);
            else if (Job.Kind == LevelKind)
                Level = result.Deserialize<BalanceLevelResultModel>(_jsonOptions);
            else if (Job.Kind == CompareKind)
                Compare = result.Deserialize<BalanceCompareResultModel>(_jsonOptions);
            else if (Job.Kind == SweepKind)
                Sweep = result.Deserialize<BalanceSweepResultModel>(_jsonOptions);

            return applied;
        }

        private async Task<bool> LoadRunAsync(string runId)
        {
            var run = await LoadAsync<BalanceRunModel>("/admin/balance/history/" + Escape(runId));

            if (run == null || ApplyState(run.Payload) == false)
            {
                ShowError("Прогон " + runId + " не найден в истории или в нём нет сетапа.");

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

        private bool CollectAllEdits()
        {
            ValidateEdits(Edits, "Правка");
            ValidateEdits(EditsB, "Правка B");

            if (EditErrors.Count == 0)
                return true;

            ShowError("Черновые правки заполнены не до конца: " + string.Join("; ", EditErrors) + ".");

            return false;
        }

        private void ValidateEdits(List<BalanceConfigEditModel> edits, string label)
        {
            for (int i = 0; i < edits.Count; i++)
            {
                var edit = edits[i];

                if (IsEmpty(edit))
                    continue;

                if (string.IsNullOrWhiteSpace(edit.Sheet) || string.IsNullOrWhiteSpace(edit.Row) || string.IsNullOrWhiteSpace(edit.Column))
                    EditErrors.Add(label + " " + (i + 1) + " — нужны лист, строка и колонка");
            }
        }

        private List<BalanceConfigEditModel> CleanEdits(List<BalanceConfigEditModel> edits)
        {
            var result = new List<BalanceConfigEditModel>(edits.Count);

            for (int i = 0; i < edits.Count; i++)
            {
                var edit = edits[i];

                if (IsEmpty(edit))
                    continue;

                result.Add(new BalanceConfigEditModel
                {
                    Sheet = (edit.Sheet ?? string.Empty).Trim(),
                    Row = (edit.Row ?? string.Empty).Trim(),
                    Column = (edit.Column ?? string.Empty).Trim(),
                    Value = (edit.Value ?? string.Empty).Trim(),
                });
            }

            return result;
        }

        private bool IsEmpty(BalanceConfigEditModel edit)
        {
            return string.IsNullOrWhiteSpace(edit.Row) && string.IsNullOrWhiteSpace(edit.Column) && string.IsNullOrWhiteSpace(edit.Value);
        }

        private List<BalanceConfigEditModel> ParseLegacyEdits(string text)
        {
            var edits = new List<BalanceConfigEditModel>();
            var lines = (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Trim().Split(LegacyEditSeparator);

                if (parts.Length == 4)
                    edits.Add(new BalanceConfigEditModel { Sheet = parts[0].Trim(), Row = parts[1].Trim(), Column = parts[2].Trim(), Value = parts[3].Trim() });
            }

            return edits;
        }

        private object CreateFightRequest()
        {
            return new
            {
                version = Version,
                edits = CleanEdits(Edits),
                setup = CreateSetup(),
                target = CreateTarget(),
                seed = Seed,
                battles = Math.Max(1, Battles),
                includeLog = true,
                scenario = SerializeState(),
            };
        }

        private object CreateLevelRequest()
        {
            return new
            {
                version = Version,
                edits = CleanEdits(Edits),
                setup = CreateSetup(),
                storyLevelId = LevelId,
                seed = (long)Seed,
                runs = LevelRuns,
                perkPolicy = PerkPolicy,
                perkPriority = ParseIds(PerkPriorityText),
                forkPolicy = ForkPolicy,
                scenario = SerializeState(),
            };
        }

        private string SerializeState()
        {
            return JsonSerializer.Serialize(new BalanceFormState
            {
                Tab = Tab,
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
                Edits = CleanEdits(Edits),
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
                EditsB = CleanEdits(EditsB),
                CompareKind = CompareWhat,
                SweepKind = SweepWhat,
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

            Tab = string.IsNullOrEmpty(state.Tab) ? Tab : state.Tab;
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
            Edits = state.Edits.Count != 0 ? state.Edits : ParseLegacyEdits(state.EditsText);
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
            EditsB = state.EditsB.Count != 0 ? state.EditsB : ParseLegacyEdits(state.EditsBText);
            CompareWhat = state.CompareKind;
            SweepWhat = state.SweepKind;
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
                .Append("\" text-anchor=\"").Append(anchor)
                .Append("\" class=\"axis\">").Append(WebUtility.HtmlEncode(text)).Append("</text>");
        }

        private void NormalizeInput()
        {
            Tab = IsKnownTab(Tab) ? Tab : BattleTab;
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
            VersionB = VersionB ?? string.Empty;
            CompareWhat = CompareWhat == LevelKind ? LevelKind : SeriesKind;
            SweepWhat = SweepWhat == LevelKind ? LevelKind : SeriesKind;
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
            Edits = Edits ?? new List<BalanceConfigEditModel>();
            EditsB = EditsB ?? new List<BalanceConfigEditModel>();
        }

        private bool IsKnownTab(string? tab)
        {
            return tab == BattleTab || tab == LevelTab || tab == CompareTab || tab == SweepTab || tab == HistoryTab;
        }

        private string EditsWord(int count)
        {
            var lastTwo = count % 100;
            var last = count % 10;

            if (11 <= lastTwo && lastTwo <= 14)
                return "правок";

            if (last == 1)
                return "правка";

            if (2 <= last && last <= 4)
                return "правки";

            return "правок";
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

        private void ApplySetup(BalanceSetupModel setup)
        {
            CharacterId = setup.CharacterId;
            PromoteLevel = setup.PromoteLevel;
            Summons = new List<BalanceSummonModel>(setup.Summons);
            Equipment = new List<BalanceEquipmentModel>(setup.Equipment);
            AccountBonusesText = FormatBonuses(setup.AccountBonuses);
            CurrentHealth = 0f;
            RunPerksText = string.Empty;
            RunBonusesText = string.Empty;
            RunStatusesText = string.Empty;
        }

        private void ApplyProfile(PlayerProfileModel profile)
        {
            CharacterId = profile.Loadout.CharacterId;
            PromoteLevel = 1;
            Summons = new List<BalanceSummonModel>();
            Equipment = new List<BalanceEquipmentModel>();
            CurrentHealth = 0f;
            RunPerksText = string.Empty;
            RunBonusesText = string.Empty;
            RunStatusesText = string.Empty;

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

        private string FormatBonuses(List<BalanceBonusModel> bonuses)
        {
            var parts = new List<string>(bonuses.Count);

            for (int i = 0; i < bonuses.Count; i++)
                parts.Add(bonuses[i].Id.ToString(CultureInfo.InvariantCulture) + ":" + bonuses[i].Count.ToString(CultureInfo.InvariantCulture));

            return string.Join(", ", parts);
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
            return $"character {CharacterId} promote {PromoteLevel} fight {Fight} enemies {EnemiesText} version {(Version.Length == 0 ? "master" : Version)} edits {CleanEdits(Edits).Count}";
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

        private double ParseDouble(string text)
        {
            return double.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
    }
}

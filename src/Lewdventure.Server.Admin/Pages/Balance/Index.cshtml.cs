using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Balance
{
    internal sealed class IndexModel : AdminPageModel
    {
        public const int SummonSlots = 3;
        public const int EquipmentSlots = 3;

        private const char TargetSeparator = '|';

        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        [BindProperty]
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

        public List<BalanceVersionModel> Versions { get; private set; } = new();

        public BalanceCatalogModel Catalog { get; private set; } = new();

        public List<BalanceLayerModel> Layers { get; private set; } = new();

        public BalanceBattleResultModel? Battle { get; private set; }

        public BalanceSeriesResultModel? Series { get; private set; }

        public async Task OnGetAsync()
        {
            await LoadReferenceAsync();

            if (CharacterId == 0 && 0 < Catalog.Items.Characters.Count)
                CharacterId = Catalog.Items.Characters[0].Id;

            if (Fight.Length == 0)
                Fight = FirstFight();

            FillSlots();
        }

        public async Task<IActionResult> OnPostCharacteristicsAsync()
        {
            await LoadReferenceAsync();

            var result = await ExecuteAsync<List<BalanceLayerModel>>(HttpMethod.Post, "/admin/balance/characteristics", CreateRequest(), "balance.characteristics", DescribeSetup(), "Характеристики посчитаны.");

            if (result.Data != null)
                Layers = result.Data;

            FillSlots();

            return Page();
        }

        public async Task<IActionResult> OnPostBattleAsync()
        {
            await LoadReferenceAsync();

            var result = await ExecuteAsync<BalanceBattleResultModel>(HttpMethod.Post, "/admin/balance/battle", CreateRequest(), "balance.battle", DescribeSetup() + " seed " + Seed, "Бой посчитан.");

            Battle = result.Data;

            FillSlots();

            return Page();
        }

        public async Task<IActionResult> OnPostSeriesAsync()
        {
            await LoadReferenceAsync();

            var result = await ExecuteAsync<BalanceSeriesResultModel>(HttpMethod.Post, "/admin/balance/series", CreateRequest(), "balance.series", DescribeSetup() + " x" + Battles, "Серия посчитана.");

            Series = result.Data;

            FillSlots();

            return Page();
        }

        public async Task<IActionResult> OnPostLoadPlayerAsync()
        {
            await LoadReferenceAsync();

            var userId = LoadUserId.Trim();
            var profile = userId.Length == 0 ? null : await LoadAsync<PlayerProfileModel>("/admin/player/" + Escape(userId));

            if (profile == null)
            {
                ErrorMessage = "Профиль игрока " + userId + " не найден на окружении " + CurrentEnvironment + ".";
                FillSlots();

                return Page();
            }

            ApplyProfile(profile);

            var run = await GameAdminClient.GetAsync<QaRunModel>(CurrentEnvironment, "/admin/qa/players/" + Escape(userId) + "/run", LoginName, HttpContext.RequestAborted);

            if (run.IsSuccess && run.Data != null)
                ApplyRun(run.Data);

            StatusMessage = "Сетап взят у игрока " + userId + (run.IsSuccess && run.Data != null ? " вместе с забегом." : ", забега нет.");
            FillSlots();

            return Page();
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

            var versions = await LoadAsync<List<BalanceVersionModel>>("/admin/balance/versions");

            if (versions != null)
                Versions = versions;

            var catalog = await LoadAsync<BalanceCatalogModel>("/admin/balance/catalog" + (Version.Length == 0 ? string.Empty : "?version=" + Escape(Version)));

            if (catalog != null)
                Catalog = catalog;
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
            Summons = Summons ?? new List<BalanceSummonModel>();
            Equipment = Equipment ?? new List<BalanceEquipmentModel>();
        }

        private object CreateRequest()
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

            return new
            {
                version = Version,
                setup,
                target = CreateTarget(),
                seed = Seed,
                battles = Battles,
                includeLog = true,
            };
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

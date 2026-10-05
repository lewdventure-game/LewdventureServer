using Microsoft.AspNetCore.Mvc;
using Server.Admin.Analytics;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Players
{
    internal sealed class IndexModel : AdminPageModel
    {
        private const string UserIdPrefix = "usr_";

        private readonly AnalyticsReportService _analyticsReportService;

        public IndexModel(
            AdminAuditLog auditLog,
            AdminEnvironmentSelector environmentSelector,
            GameAdminClient gameAdminClient,
            AnalyticsReportService analyticsReportService)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
            _analyticsReportService = analyticsReportService;
        }

        public string Query { get; private set; } = string.Empty;

        public string UserId { get; private set; } = string.Empty;

        public PlayerProfileModel? Profile { get; private set; }

        public ExperimentPlayerModel? Assignment { get; private set; }

        public QaAccountModel? Account { get; private set; }

        public QaStatusModel Status { get; private set; } = new();

        public CheatCatalogModel Catalog { get; private set; } = new();

        public List<QaAccountModel> QaAccounts { get; private set; } = new();

        public List<QaMatchModel> Matches { get; private set; } = new();

        public List<PlayerLedgerModel> Ledger { get; private set; } = new();

        public List<EventRow> Events { get; private set; } = new();

        public List<string> EventErrors { get; } = new();

        public bool CheatsEnabled => Status.CheatsEnabled;

        public async Task<IActionResult> OnGetAsync(string? userId, string? query)
        {
            var status = await LoadAsync<QaStatusModel>("/admin/qa/status");

            if (status != null)
                Status = status;

            Query = query == null ? string.Empty : query.Trim();
            UserId = userId == null ? string.Empty : userId.Trim();

            if (UserId.Length == 0 && Query.StartsWith(UserIdPrefix, StringComparison.Ordinal))
                UserId = Query;

            if (UserId.Length == 0 && Query.Length != 0)
            {
                var matches = await LoadAsync<List<QaMatchModel>>("/admin/qa/find?query=" + Escape(Query));

                if (matches != null && matches.Count == 1)
                    return RedirectToPage(new { userId = matches[0].UserId });

                if (matches != null)
                    Matches = matches;

                if (matches != null && matches.Count == 0)
                    LoadErrors.Add("Никого не нашли. Ищите по userId, псевдониму тестового аккаунта или deviceId.");
            }

            if (UserId.Length == 0)
            {
                var accounts = await LoadAsync<List<QaAccountModel>>("/admin/qa/players");

                if (accounts != null)
                    QaAccounts = accounts;

                return Page();
            }

            var path = "/admin/player/" + Escape(UserId);
            var ledger = await LoadAsync<List<PlayerLedgerModel>>(path + "/ledger?limit=50");
            var catalog = await LoadAsync<CheatCatalogModel>("/admin/qa/catalog");

            Profile = await LoadAsync<PlayerProfileModel>(path);
            Assignment = await LoadAsync<ExperimentPlayerModel>("/admin/experiments/player/" + Escape(UserId));
            Account = await LoadAsync<QaAccountModel>("/admin/qa/players/" + Escape(UserId));

            if (catalog != null)
                Catalog = catalog;

            if (ledger != null)
                Ledger = ledger;

            Events = await _analyticsReportService.LoadPlayerEventsAsync(CurrentEnvironment, UserId, EventErrors, HttpContext.RequestAborted);

            return Page();
        }

        public string DescribeCharacter(int id)
        {
            return Describe(Catalog.Characters, id);
        }

        public string DescribeSummon(int id)
        {
            return Describe(Catalog.Summons, id);
        }

        public string DescribeEquipment(int id)
        {
            return Describe(Catalog.Equipment, id);
        }

        public int FindMaxLevel(List<CheatCatalogItemModel> items, int id)
        {
            var item = Find(items, id);

            return item == null ? 0 : item.MaxLevel;
        }

        public int FindMaxMastery(int summonId)
        {
            var item = Find(Catalog.Summons, summonId);

            return item == null ? 0 : item.MaxMastery;
        }

        public async Task<IActionResult> OnPostGrantAsync(string userId, string? rewards, string? reason)
        {
            var body = new
            {
                rewards = rewards == null ? string.Empty : rewards.Trim(),
                reason = string.IsNullOrWhiteSpace(reason) ? LoginName : reason.Trim(),
                requestId = Guid.NewGuid().ToString("N"),
            };

            await ExecuteAsync<PlayerProfileModel>(HttpMethod.Post, "/admin/player/" + Escape(userId) + "/grant", body, "player.grant", userId + " " + body.rewards, "Награда выдана.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostGrantItemAsync(string userId, string? item, int count)
        {
            var rewards = (item ?? string.Empty).Trim() + ":" + count;
            var body = new
            {
                rewards,
                reason = LoginName,
                requestId = Guid.NewGuid().ToString("N"),
            };

            await ExecuteAsync<PlayerProfileModel>(HttpMethod.Post, "/admin/player/" + Escape(userId) + "/grant", body, "player.grant", userId + " " + rewards, "Награда выдана: " + rewards + ".");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostMarkAsync(string userId, string? alias)
        {
            await ExecuteAsync<object>(HttpMethod.Post, "/admin/qa/players/" + Escape(userId) + "/mark", new { alias = alias ?? string.Empty }, "qa.mark", userId + " " + alias, "Аккаунт помечен как тестовый.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostUnmarkAsync(string userId)
        {
            await ExecuteAsync<object>(HttpMethod.Delete, "/admin/qa/players/" + Escape(userId) + "/mark", null, "qa.unmark", userId, "Метка тестового аккаунта снята.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostPresetAsync(string userId, string? preset, int amount)
        {
            var body = new { preset = preset ?? string.Empty, amount };

            await ExecuteCheatAsync(userId, "/preset", body, "cheat.preset", preset ?? string.Empty, "Выдано.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostResourceAsync(string userId, string? key, long amount)
        {
            var body = new { key = key ?? string.Empty, amount };

            await ExecuteCheatAsync(userId, "/resource", body, "cheat.resource", key + "=" + amount, "Ресурс установлен.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostFlagAsync(string userId, string? key, int value)
        {
            var body = new { key = key ?? string.Empty, value };

            await ExecuteCheatAsync(userId, "/flag", body, "cheat.flag", key + "=" + value, "Флаг установлен.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostCharacterAsync(string userId, int characterId, int promoteLevel)
        {
            await ExecuteCheatAsync(userId, "/character/" + characterId, new { promoteLevel }, "cheat.character", characterId + " promote " + promoteLevel, "Персонаж обновлён.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostSummonAsync(string userId, int summonId, int? level, int? masteryLevel, int? skillLevel)
        {
            var body = new { level, masteryLevel, skillLevel };

            await ExecuteCheatAsync(userId, "/summon/" + summonId, body, "cheat.summon", $"{summonId} level {level} mastery {masteryLevel} skill {skillLevel}", "Саммон обновлён.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostEquipmentAsync(string userId, string? instanceId, int level)
        {
            var target = instanceId ?? string.Empty;

            await ExecuteCheatAsync(userId, "/equipment/" + Escape(target), new { level }, "cheat.equipment", target + " level " + level, "Снаряжение обновлено.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostMaxAsync(string userId)
        {
            await ExecuteCheatAsync(userId, "/max", new { }, "cheat.max", string.Empty, "Всё прокачано до максимума.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostAbandonAsync(string userId)
        {
            await ExecuteCheatAsync(userId, "/run/abandon", new { }, "cheat.abandon", string.Empty, "Забег прерван.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostResetAsync(string userId)
        {
            await ExecuteCheatAsync(userId, "/reset", new { }, "cheat.reset", string.Empty, "Прогресс сброшен. При следующем входе игрок получит стартовый набор.");

            return RedirectToPage(new { userId });
        }

        public async Task<IActionResult> OnPostDeleteAsync(string userId, string? confirm)
        {
            var denied = RequireAdmin();

            if (denied != null)
                return RedirectToPage(new { userId });

            var result = await ExecuteAsync<object>(HttpMethod.Delete, "/admin/player/" + Escape(userId) + "?confirm=" + Escape(confirm ?? string.Empty), null, "player.delete", userId, "Данные игрока удалены.");

            return result.IsSuccess ? RedirectToPage() : RedirectToPage(new { userId });
        }

        private async Task ExecuteCheatAsync(string userId, string path, object body, string action, string details, string successMessage)
        {
            var target = details.Length == 0 ? userId : userId + " " + details;

            await ExecuteAsync<object>(HttpMethod.Post, "/admin/qa/players/" + Escape(userId) + "/cheats" + path, body, action, target, successMessage);
        }

        private string Describe(List<CheatCatalogItemModel> items, int id)
        {
            var item = Find(items, id);

            if (item == null || item.Name.Length == 0)
                return id.ToString();

            return id + " · " + item.Name;
        }

        private CheatCatalogItemModel? Find(List<CheatCatalogItemModel> items, int id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                    return items[i];
            }

            return null;
        }
    }
}

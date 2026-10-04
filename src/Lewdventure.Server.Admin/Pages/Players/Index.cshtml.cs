using Microsoft.AspNetCore.Mvc;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Players
{
    internal sealed class IndexModel : AdminPageModel
    {
        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        public string UserId { get; private set; } = string.Empty;

        public PlayerProfileModel? Profile { get; private set; }

        public ExperimentPlayerModel? Assignment { get; private set; }

        public List<PlayerLedgerModel> Ledger { get; private set; } = new();

        public async Task OnGetAsync(string? userId)
        {
            UserId = userId == null ? string.Empty : userId.Trim();

            if (UserId.Length == 0)
                return;

            var path = "/admin/player/" + Escape(UserId);
            var ledger = await LoadAsync<List<PlayerLedgerModel>>(path + "/ledger?limit=50");

            Profile = await LoadAsync<PlayerProfileModel>(path);
            Assignment = await LoadAsync<ExperimentPlayerModel>("/admin/experiments/player/" + Escape(UserId));

            if (ledger != null)
                Ledger = ledger;
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

        public async Task<IActionResult> OnPostDeleteAsync(string userId, string? confirm)
        {
            var denied = RequireAdmin();

            if (denied != null)
                return RedirectToPage(new { userId });

            var result = await ExecuteAsync<object>(HttpMethod.Delete, "/admin/player/" + Escape(userId) + "?confirm=" + Escape(confirm ?? string.Empty), null, "player.delete", userId, "Данные игрока удалены.");

            return result.IsSuccess ? RedirectToPage() : RedirectToPage(new { userId });
        }
    }
}

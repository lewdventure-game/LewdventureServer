using Microsoft.AspNetCore.Mvc;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Configs
{
    internal sealed class IndexModel : AdminPageModel
    {
        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        public ConfigStatusModel? Status { get; private set; }

        public List<ConfigSnapshotModel> Snapshots { get; private set; } = new();

        public async Task OnGetAsync()
        {
            Status = await LoadAsync<ConfigStatusModel>("/admin/config/status");

            var snapshots = await LoadAsync<List<ConfigSnapshotModel>>("/admin/config/snapshots?limit=100");

            if (snapshots != null)
                Snapshots = snapshots;
        }

        public async Task<IActionResult> OnPostActivateAsync(string version)
        {
            var denied = RequireAdmin();

            if (denied != null)
                return denied;

            await ExecuteAsync<ConfigPublishModel>(HttpMethod.Post, "/admin/config/activate", new { version, reason = "admin panel" }, "config.activate", version, $"{Shorten(version)} теперь мастер.");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostReloadAsync()
        {
            var denied = RequireAdmin();

            if (denied != null)
                return denied;

            await ExecuteAsync<ConfigPublishModel>(HttpMethod.Post, "/admin/config/reload", null, "config.reload", string.Empty, "Активная версия перечитана.");

            return RedirectToPage();
        }

        public string Shorten(string version)
        {
            return version.StartsWith("sha256:", StringComparison.Ordinal) && 19 <= version.Length ? "cfg-" + version.Substring(7, 12) : version;
        }
    }
}

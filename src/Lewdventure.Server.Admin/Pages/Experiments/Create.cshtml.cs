using Microsoft.AspNetCore.Mvc;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Experiments
{
    internal sealed class CreateModel : AdminPageModel
    {
        private const int GroupRows = 6;

        public CreateModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        [BindProperty]
        public string ExperimentId { get; set; } = string.Empty;

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public List<GroupInput> Groups { get; set; } = new();

        public List<ConfigSnapshotModel> Snapshots { get; private set; } = new();

        public string MasterVersion { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            var denied = RequireAdminPage();

            if (denied != null)
                return denied;

            await LoadSnapshotsAsync();
            FillRows();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var denied = RequireAdminPage();

            if (denied != null)
                return denied;

            var body = new
            {
                id = Clean(ExperimentId),
                name = Clean(Name),
                description = Clean(Description),
                groups = CollectGroups(),
            };
            var result = await ExecuteAsync<ExperimentMutationModel>(HttpMethod.Post, "/admin/experiments", body, "experiment.create", body.id, "Черновик создан.");

            if (result.IsSuccess)
                return RedirectToPage("/Experiments/Details", new { id = body.id });

            await LoadSnapshotsAsync();
            FillRows();

            return Page();
        }

        public string Describe(ConfigSnapshotModel snapshot)
        {
            var shortVersion = snapshot.Version.Length < 19 ? snapshot.Version : "cfg-" + snapshot.Version.Substring(7, 12);
            var master = string.Equals(snapshot.Version, MasterVersion, StringComparison.Ordinal) ? " · мастер" : string.Empty;

            return $"{shortVersion} · {snapshot.CreatedAt:yyyy-MM-dd HH:mm} · {snapshot.CreatedBy}{master}";
        }

        private IActionResult? RequireAdminPage()
        {
            if (IsAdmin)
                return null;

            ErrorMessage = "Создавать эксперименты может только роль admin.";

            return RedirectToPage("/Experiments/Index");
        }

        private async Task LoadSnapshotsAsync()
        {
            var status = await LoadAsync<ConfigStatusModel>("/admin/config/status");
            var snapshots = await LoadAsync<List<ConfigSnapshotModel>>("/admin/config/snapshots?limit=50");

            MasterVersion = status == null ? string.Empty : status.ActiveVersion;

            if (snapshots != null)
                Snapshots = snapshots;
        }

        private void FillRows()
        {
            while (Groups.Count < GroupRows)
                Groups.Add(new GroupInput());
        }

        private List<object> CollectGroups()
        {
            var groups = new List<object>();

            for (int i = 0; i < Groups.Count; i++)
            {
                var group = Groups[i];

                if (Clean(group.Id).Length == 0 && Clean(group.SnapshotVersion).Length == 0)
                    continue;

                groups.Add(new
                {
                    id = Clean(group.Id),
                    name = Clean(group.Name),
                    snapshotVersion = Clean(group.SnapshotVersion),
                    percent = group.Percent,
                    newPlayersOnly = group.NewPlayersOnly,
                    countries = SplitCountries(Clean(group.Countries)),
                });
            }

            return groups;
        }

        private string Clean(string? value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private List<string> SplitCountries(string countries)
        {
            var result = new List<string>();
            var parts = countries.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (int i = 0; i < parts.Length; i++)
                result.Add(parts[i].ToUpperInvariant());

            return result;
        }
    }
}

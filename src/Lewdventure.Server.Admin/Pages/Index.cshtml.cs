using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages
{
    internal sealed class IndexModel : AdminPageModel
    {
        private const string RunningStatus = "running";

        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        public ConfigStatusModel? Status { get; private set; }

        public List<ExperimentModel> Running { get; } = new();

        public async Task OnGetAsync()
        {
            Status = await LoadAsync<ConfigStatusModel>("/admin/config/status");

            var experiments = await LoadAsync<List<ExperimentModel>>("/admin/experiments?limit=50");

            if (experiments == null)
                return;

            for (int i = 0; i < experiments.Count; i++)
            {
                if (string.Equals(experiments[i].Status, RunningStatus, StringComparison.Ordinal))
                    Running.Add(experiments[i]);
            }
        }
    }
}

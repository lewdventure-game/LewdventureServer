using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Experiments
{
    internal sealed class IndexModel : AdminPageModel
    {
        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        public List<ExperimentModel> Experiments { get; private set; } = new();

        public async Task OnGetAsync()
        {
            var experiments = await LoadAsync<List<ExperimentModel>>("/admin/experiments?limit=200");

            if (experiments != null)
                Experiments = experiments;
        }

        public long CountParticipants(ExperimentModel experiment)
        {
            var total = 0L;

            for (int i = 0; i < experiment.Groups.Count; i++)
                total += experiment.Groups[i].Participants ?? 0;

            return total;
        }
    }
}

using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Errors
{
    internal sealed class IndexModel : AdminPageModel
    {
        private const int Limit = 200;

        public IndexModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
        }

        public string UserId { get; private set; } = string.Empty;

        public string CorrelationId { get; private set; } = string.Empty;

        public QaStatusModel Status { get; private set; } = new();

        public List<ServerErrorModel> Errors { get; private set; } = new();

        public List<RequestTraceModel> Requests { get; private set; } = new();

        public async Task OnGetAsync(string? userId, string? correlationId)
        {
            UserId = userId == null ? string.Empty : userId.Trim();
            CorrelationId = correlationId == null ? string.Empty : correlationId.Trim();

            var status = await LoadAsync<QaStatusModel>("/admin/qa/status");

            if (status != null)
                Status = status;

            var errors = await LoadAsync<List<ServerErrorModel>>($"/admin/qa/errors?limit={Limit}&userId={Escape(UserId)}&correlationId={Escape(CorrelationId)}");

            if (errors != null)
                Errors = errors;

            if (CorrelationId.Length == 0)
                return;

            var requests = await LoadAsync<List<RequestTraceModel>>("/admin/qa/requests?correlationId=" + Escape(CorrelationId));

            if (requests != null)
                Requests = requests;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Server.Admin.Analytics;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Experiments
{
    internal sealed class DetailsModel : AdminPageModel
    {
        private readonly AnalyticsReportService _analyticsReportService;

        public DetailsModel(
            AdminAuditLog auditLog,
            AdminEnvironmentSelector environmentSelector,
            GameAdminClient gameAdminClient,
            AnalyticsReportService analyticsReportService)
            : base(auditLog, environmentSelector, gameAdminClient)
        {
            _analyticsReportService = analyticsReportService;
        }

        public ExperimentModel? Experiment { get; private set; }

        public List<ExperimentChangeModel> Changes { get; private set; } = new();

        public string MasterVersion { get; private set; } = string.Empty;

        public List<ExperimentGroupStats> Stats { get; private set; } = new();

        public List<string> StatsErrors { get; } = new();

        public async Task OnGetAsync(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                LoadErrors.Add("Не указан id эксперимента.");

                return;
            }

            var path = "/admin/experiments/" + Escape(id);
            var status = await LoadAsync<ConfigStatusModel>("/admin/config/status");
            var changes = await LoadAsync<List<ExperimentChangeModel>>(path + "/changes?limit=100");

            Experiment = await LoadAsync<ExperimentModel>(path);
            MasterVersion = status == null ? string.Empty : status.ActiveVersion;

            if (changes != null)
                Changes = changes;

            if (Experiment != null && Experiment.Status != "draft")
                Stats = await _analyticsReportService.BuildExperimentStatsAsync(CurrentEnvironment, Experiment.Id, StatsErrors, HttpContext.RequestAborted);
        }

        public async Task<IActionResult> OnPostStartAsync(string id, string? reason)
        {
            return await RunAsync(id, "/start", new { reason = reason ?? string.Empty }, "experiment.start", id, "Эксперимент запущен.");
        }

        public async Task<IActionResult> OnPostFinishAsync(string id, string? reason)
        {
            return await RunAsync(id, "/finish", new { reason = reason ?? string.Empty }, "experiment.finish", id, "Эксперимент закончен, все на мастере.");
        }

        public async Task<IActionResult> OnPostFreezeAsync(string id, string groupId)
        {
            return await RunAsync(id, "/groups/" + Escape(groupId) + "/freeze", new { reason = string.Empty }, "experiment.freeze", id + "/" + groupId, $"Набор в группу {groupId} остановлен.");
        }

        public async Task<IActionResult> OnPostRemoveAsync(string id, string groupId)
        {
            return await RunAsync(id, "/groups/" + Escape(groupId) + "/remove", new { reason = string.Empty }, "experiment.remove", id + "/" + groupId, $"Группа {groupId} убрана, её игроки на мастере.");
        }

        public async Task<IActionResult> OnPostRolloutAsync(string id, string groupId)
        {
            return await RunAsync(id, "/rollout", new { groupId, reason = string.Empty }, "experiment.rollout", id + "/" + groupId, $"Снапшот группы {groupId} стал мастером, эксперимент закончен.");
        }

        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            var denied = RequireAdmin();

            if (denied != null)
                return denied;

            var result = await ExecuteAsync<ExperimentMutationModel>(HttpMethod.Delete, "/admin/experiments/" + Escape(id), null, "experiment.delete", id, "Черновик удалён.");

            if (result.IsSuccess)
                return RedirectToPage("/Experiments/Index");

            return RedirectToPage(new { id });
        }

        public string Shorten(string version)
        {
            return version.StartsWith("sha256:", StringComparison.Ordinal) && 19 <= version.Length ? "cfg-" + version.Substring(7, 12) : version;
        }

        private async Task<IActionResult> RunAsync(string id, string suffix, object body, string action, string target, string successMessage)
        {
            var denied = RequireAdmin();

            if (denied != null)
                return RedirectToPage(new { id });

            await ExecuteAsync<ExperimentMutationModel>(HttpMethod.Post, "/admin/experiments/" + Escape(id) + suffix, body, action, target, successMessage);

            return RedirectToPage(new { id });
        }
    }
}

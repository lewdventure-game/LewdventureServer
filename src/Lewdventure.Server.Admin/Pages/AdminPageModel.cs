using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Admin.Accounts;
using Server.Admin.Backend;
using Server.Admin.Backend.Models;

namespace Server.Admin.Pages
{
    internal abstract class AdminPageModel : PageModel
    {
        public const string EnvironmentKey = "Environment";
        public const string EnvironmentsKey = "Environments";

        private const int RevisionLength = 7;

        private readonly AdminAuditLog _auditLog;
        private readonly AdminEnvironmentSelector _environmentSelector;

        protected AdminPageModel(AdminAuditLog auditLog, AdminEnvironmentSelector environmentSelector, GameAdminClient gameAdminClient)
        {
            _auditLog = auditLog;
            _environmentSelector = environmentSelector;
            GameAdminClient = gameAdminClient;
        }

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public string CurrentEnvironment { get; private set; } = string.Empty;

        public string LoginName => User.Identity == null || User.Identity.Name == null ? string.Empty : User.Identity.Name;

        public bool IsAdmin => User.IsInRole(AdminRoles.Admin);

        public List<string> LoadErrors { get; } = new();

        public string VersionWarning { get; private set; } = string.Empty;

        protected GameAdminClient GameAdminClient { get; }

        public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            CurrentEnvironment = _environmentSelector.Resolve(HttpContext);
            ViewData[EnvironmentKey] = CurrentEnvironment;
            ViewData[EnvironmentsKey] = _environmentSelector.CollectNames();
        }

        protected async Task<T?> LoadAsync<T>(string path)
        {
            var result = await GameAdminClient.GetAsync<T>(CurrentEnvironment, path, LoginName, HttpContext.RequestAborted);

            if (result.IsSuccess == false)
                AddLoadErrors(result.Errors);

            return result.Data;
        }

        protected void AddLoadErrors(List<string> errors)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                if (LoadErrors.Contains(errors[i]) == false)
                    LoadErrors.Add(errors[i]);
            }
        }

        protected async Task<QaStatusModel> LoadQaStatusAsync()
        {
            var status = await LoadAsync<QaStatusModel>("/admin/qa/status");

            if (status == null)
                return new QaStatusModel();

            VersionWarning = DescribeVersionMismatch(status.Version);

            return status;
        }

        protected async Task<GameAdminResult<T>> ExecuteAsync<T>(HttpMethod method, string path, object? body, string action, string target, string successMessage)
        {
            var result = await GameAdminClient.SendAsync<T>(CurrentEnvironment, method, path, body, LoginName, HttpContext.RequestAborted);

            _auditLog.Append(LoginName, CurrentEnvironment, action, target, result.IsSuccess);

            if (result.IsSuccess)
            {
                StatusMessage = result.Warnings.Count == 0 ? successMessage : successMessage + " Предупреждения: " + string.Join("; ", result.Warnings);
                ErrorMessage = null;
            }
            else
            {
                ErrorMessage = string.Join("; ", result.Errors);
                StatusMessage = null;
            }

            return result;
        }

        protected async Task<List<string>> LoadQaUserIdsAsync()
        {
            var userIds = new List<string>();
            var result = await GameAdminClient.GetAsync<List<QaAccountModel>>(CurrentEnvironment, "/admin/qa/players", LoginName, HttpContext.RequestAborted);

            if (result.IsSuccess == false || result.Data == null)
                return userIds;

            for (int i = 0; i < result.Data.Count; i++)
                userIds.Add(result.Data[i].UserId);

            return userIds;
        }

        protected IActionResult? RequireAdmin()
        {
            if (IsAdmin)
                return null;

            ErrorMessage = "Действие доступно только роли admin.";

            return RedirectToPage();
        }

        protected string Escape(string value)
        {
            return Uri.EscapeDataString(value);
        }

        private string DescribeVersionMismatch(string serverVersion)
        {
            var adminRevision = ReadRevision(ReadAdminVersion());
            var serverRevision = ReadRevision(serverVersion);

            if (adminRevision.Length == 0 || serverRevision.Length == 0 || string.Equals(adminRevision, serverRevision, StringComparison.Ordinal))
                return string.Empty;

            return $"Админка собрана из {adminRevision}, сервер {CurrentEnvironment} — из {serverRevision}. Новые разделы админки могут не работать, пока сервер не обновят.";
        }

        private string ReadAdminVersion()
        {
            var attribute = typeof(AdminPageModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

            return attribute == null ? string.Empty : attribute.InformationalVersion;
        }

        private string ReadRevision(string version)
        {
            var separator = version.IndexOf('+');

            if (separator < 0 || version.Length <= separator + 1)
                return string.Empty;

            var revision = version.Substring(separator + 1);

            return RevisionLength < revision.Length ? revision.Substring(0, RevisionLength) : revision;
        }
    }
}

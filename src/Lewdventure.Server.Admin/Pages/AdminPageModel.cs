using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Admin.Accounts;
using Server.Admin.Backend;

namespace Server.Admin.Pages
{
    internal abstract class AdminPageModel : PageModel
    {
        public const string EnvironmentKey = "Environment";
        public const string EnvironmentsKey = "Environments";

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
                LoadErrors.AddRange(result.Errors);

            return result.Data;
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
    }
}

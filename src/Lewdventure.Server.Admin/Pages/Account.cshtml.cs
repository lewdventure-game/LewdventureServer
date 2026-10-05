using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Admin.Accounts;
using Server.Admin.Backend;

namespace Server.Admin.Pages
{
    internal sealed class AccountModel : PageModel
    {
        public const int MinPasswordLength = 10;
        public const int MaxPasswordLength = 128;

        private readonly AdminAccountStore _accountStore;
        private readonly AdminAuditLog _auditLog;
        private readonly AdminLoginThrottle _loginThrottle;
        private readonly AdminPasswordHasher _passwordHasher;

        public AccountModel(AdminAccountStore accountStore, AdminAuditLog auditLog, AdminLoginThrottle loginThrottle, AdminPasswordHasher passwordHasher)
        {
            _accountStore = accountStore;
            _auditLog = auditLog;
            _loginThrottle = loginThrottle;
            _passwordHasher = passwordHasher;
        }

        [BindProperty]
        public string? CurrentPassword { get; set; }

        [BindProperty]
        public string? NewPassword { get; set; }

        [BindProperty]
        public string? ConfirmPassword { get; set; }

        public string LoginName => User.Identity == null || User.Identity.Name == null ? string.Empty : User.Identity.Name;

        public string Role => User.IsInRole(AdminRoles.Admin) ? AdminRoles.Admin : AdminRoles.Tester;

        public string Message { get; private set; } = string.Empty;

        public string Error { get; private set; } = string.Empty;

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            var account = _accountStore.Find(LoginName);

            if (account == null || account.IsDisabled)
                return Forbid();

            if (_loginThrottle.IsLocked(account.Login))
            {
                Error = "Слишком много неудачных попыток, попробуйте позже.";

                return Page();
            }

            if (_passwordHasher.Verify(account, CurrentPassword ?? string.Empty) == false)
            {
                _loginThrottle.RegisterFailure(account.Login);
                _auditLog.Append(account.Login, string.Empty, "account.password", string.Empty, false);
                Error = "Текущий пароль указан неверно.";

                return Page();
            }

            var newPassword = NewPassword ?? string.Empty;
            var validationError = Validate(newPassword, CurrentPassword ?? string.Empty);

            if (validationError.Length != 0)
            {
                Error = validationError;

                return Page();
            }

            _passwordHasher.SetPassword(account, newPassword);
            _accountStore.Upsert(account);
            _loginThrottle.RegisterSuccess(account.Login);
            _auditLog.Append(account.Login, string.Empty, "account.password", string.Empty, true);
            Message = "Пароль изменён.";

            return Page();
        }

        private string Validate(string newPassword, string currentPassword)
        {
            if (newPassword.Length < MinPasswordLength)
                return $"Новый пароль должен быть не короче {MinPasswordLength} символов.";

            if (MaxPasswordLength < newPassword.Length)
                return $"Новый пароль должен быть не длиннее {MaxPasswordLength} символов.";

            if (string.Equals(newPassword, ConfirmPassword, StringComparison.Ordinal) == false)
                return "Новый пароль и повтор не совпадают.";

            if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
                return "Новый пароль совпадает с текущим.";

            if (string.Equals(newPassword, LoginName, StringComparison.OrdinalIgnoreCase))
                return "Пароль не должен совпадать с логином.";

            return string.Empty;
        }
    }
}

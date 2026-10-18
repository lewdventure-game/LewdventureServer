using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Server.Admin.Accounts;
using Server.Admin.Backend;

namespace Server.Admin.Pages
{
    internal sealed class LoginModel : PageModel
    {
        private const string InvalidCredentials = "Неверный логин или пароль.";

        private readonly AdminAccountStore _accountStore;
        private readonly AdminAuditLog _auditLog;
        private readonly AdminLoginThrottle _loginThrottle;
        private readonly AdminPasswordHasher _passwordHasher;
        private readonly AdminPrincipalFactory _principalFactory;

        public LoginModel(AdminAccountStore accountStore, AdminAuditLog auditLog, AdminLoginThrottle loginThrottle, AdminPasswordHasher passwordHasher, AdminPrincipalFactory principalFactory)
        {
            _accountStore = accountStore;
            _auditLog = auditLog;
            _loginThrottle = loginThrottle;
            _passwordHasher = passwordHasher;
            _principalFactory = principalFactory;
        }

        [BindProperty]
        public string Login { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; set; } = string.Empty;

        public string Error { get; private set; } = string.Empty;

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var login = Login.Trim();

            if (_loginThrottle.IsLocked(login))
            {
                Error = "Слишком много неудачных попыток, попробуйте позже.";

                return Page();
            }

            var account = _accountStore.Find(login);

            if (account == null || account.IsDisabled || _passwordHasher.Verify(account, Password) == false)
            {
                _loginThrottle.RegisterFailure(login);
                _auditLog.Append(login, string.Empty, "login", string.Empty, false);
                Error = InvalidCredentials;

                return Page();
            }

            _loginThrottle.RegisterSuccess(login);
            account.LastLoginAt = DateTime.UtcNow;
            _accountStore.Upsert(account);
            _auditLog.Append(account.Login, string.Empty, "login", string.Empty, true);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, _principalFactory.Create(account));

            if (string.IsNullOrEmpty(ReturnUrl) == false && Url.IsLocalUrl(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return RedirectToPage("/Index");
        }
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Server.Admin.Accounts
{
    internal sealed class AdminSessionValidator
    {
        public const string PasswordStampClaim = "lewdventure:password-stamp";

        private readonly AdminAccountStore _accountStore;

        public AdminSessionValidator(AdminAccountStore accountStore)
        {
            _accountStore = accountStore;
        }

        public async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var principal = context.Principal;
            var login = principal?.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(login) == false)
            {
                var account = _accountStore.Find(login);

                if (account != null
                    && account.IsDisabled == false
                    && principal!.IsInRole(account.Role)
                    && string.Equals(principal.FindFirstValue(PasswordStampClaim), account.PasswordSalt, StringComparison.Ordinal))
                    return;
            }

            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

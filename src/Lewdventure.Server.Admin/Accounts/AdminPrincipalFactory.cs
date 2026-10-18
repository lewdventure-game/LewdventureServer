using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Server.Admin.Accounts
{
    internal sealed class AdminPrincipalFactory
    {
        public ClaimsPrincipal Create(AdminAccount account)
        {
            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);

            identity.AddClaim(new Claim(ClaimTypes.Name, account.Login));
            identity.AddClaim(new Claim(ClaimTypes.Role, account.Role));
            identity.AddClaim(new Claim(AdminSessionValidator.PasswordStampClaim, account.PasswordSalt));

            return new ClaimsPrincipal(identity);
        }
    }
}

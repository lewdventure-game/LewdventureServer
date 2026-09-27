using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Server.Api.Security
{
    internal sealed class PlayerIdentityReader
    {
        public string Read(ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst(JwtRegisteredClaimNames.Sub) ?? principal.FindFirst(ClaimTypes.NameIdentifier);

            return claim == null ? string.Empty : claim.Value;
        }
    }
}

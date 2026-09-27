using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Server.Infrastructure.Players;

namespace Server.Api.Security
{
    internal sealed class AccessTokenIssuer
    {
        private readonly AuthOptions _authOptions;
        private readonly TimeProvider _timeProvider;

        public AccessTokenIssuer(IOptions<AuthOptions> authOptions, TimeProvider timeProvider)
        {
            _authOptions = authOptions.Value;
            _timeProvider = timeProvider;
        }

        public AccessToken Issue(string userId)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = now.AddMinutes(_authOptions.AccessTokenMinutes);
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_authOptions.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            };
            var token = new JwtSecurityToken(
                _authOptions.Issuer,
                _authOptions.Audience,
                claims,
                now.AddMinutes(-1),
                expiresAt,
                credentials);

            return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
